using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Controllers;

[ApiController]
[Route("api/reservations/waitlist")]
[Authorize]
public class WaitlistController(
    ReservationServiceContext context,
    ICatalogServiceClient catalogServiceClient,
    IWaitlistCascadeService waitlistCascadeService,
    ILogger<WaitlistController> logger) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("userId")!);

    [HttpPost]
    public async Task<IActionResult> JoinWaitlist(JoinWaitlistRequest request)
    {
        var userId = CurrentUserId;

        var book = await catalogServiceClient.GetBookAsync(request.BookId);
        if (book.Outcome == BookLookupOutcome.ServiceUnavailable)
        {
            logger.LogError("Catalog Service unavailable while joining waitlist for book {BookId}", request.BookId);
            return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "An unexpected error occurred" });
        }

        if (book.Outcome == BookLookupOutcome.NotFound)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"Book not found with ID: {request.BookId}" });
        }

        if (book.AvailableCopies > 0)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "BOOK_AVAILABLE",
                Message = "This book currently has available copies - reserve it directly instead of joining the waitlist"
            });
        }

        var alreadyWaiting = await context.Waitlists
            .AnyAsync(w => w.BookId == request.BookId && w.UserId == userId && w.Status == WaitlistStatus.Waiting);
        if (alreadyWaiting)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "ALREADY_WAITLISTED",
                Message = "You are already on the waitlist for this book"
            });
        }

        var joinedAt = DateTime.UtcNow;
        var entry = new Waitlist
        {
            BookId = request.BookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = joinedAt,
            BookTitle = book.Title!,
            BookAuthor = book.Author!
        };

        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var position = await context.Waitlists
            .CountAsync(w => w.BookId == request.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= joinedAt);

        return StatusCode(201, new WaitlistJoinResponse
        {
            WaitlistId = entry.WaitlistId,
            BookId = entry.BookId,
            BookTitle = entry.BookTitle,
            Status = "WAITING",
            JoinedAt = entry.JoinedAt,
            Position = position
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyWaitlist()
    {
        var userId = CurrentUserId;

        var entries = await context.Waitlists
            .Where(w => w.UserId == userId && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.Notified))
            .ToListAsync();

        var items = new List<WaitlistEntryResponse>();
        foreach (var entry in entries)
        {
            int? position = null;
            if (entry.Status == WaitlistStatus.Waiting)
            {
                position = await context.Waitlists
                    .CountAsync(w => w.BookId == entry.BookId && w.Status == WaitlistStatus.Waiting && w.JoinedAt <= entry.JoinedAt);
            }

            items.Add(new WaitlistEntryResponse
            {
                WaitlistId = entry.WaitlistId,
                BookId = entry.BookId,
                BookTitle = entry.BookTitle,
                BookAuthor = entry.BookAuthor,
                Status = entry.Status.ToString().ToUpperInvariant(),
                JoinedAt = entry.JoinedAt,
                Position = position,
                NotifiedAt = entry.NotifiedAt,
                ClaimDeadline = entry.ClaimDeadline
            });
        }

        return Ok(new WaitlistListResponse { Entries = items });
    }

    [HttpDelete("{waitlistId:guid}")]
    public async Task<IActionResult> LeaveWaitlist(Guid waitlistId)
    {
        var userId = CurrentUserId;

        var entry = await context.Waitlists.FindAsync(waitlistId);
        if (entry is null || entry.UserId != userId)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = "Waitlist entry not found" });
        }

        var wasNotified = entry.Status == WaitlistStatus.Notified;
        entry.Status = WaitlistStatus.Cancelled;
        await context.SaveChangesAsync();

        if (wasNotified)
        {
            await waitlistCascadeService.OfferOrReleaseAsync(entry.BookId, DateTime.UtcNow);
        }

        return Ok(new WaitlistCancelResponse
        {
            WaitlistId = entry.WaitlistId,
            Status = "CANCELLED",
            Message = "You have been removed from the waitlist"
        });
    }
}