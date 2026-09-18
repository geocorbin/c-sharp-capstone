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
[Route("api/reservations")]
[Authorize]
public class ReservationsController(
    ReservationServiceContext context,
    IUserServiceClient userServiceClient,
    ICatalogServiceClient catalogServiceClient,
    IWaitlistCascadeService waitlistCascadeService,
    ILogger<ReservationsController> logger) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirstValue("userId")!);

    [HttpPost]
    public async Task<IActionResult> CreateReservation(CreateReservationRequest request)
    {
        var userId = CurrentUserId;

        var validation = await userServiceClient.ValidateUserAsync(userId);
        switch (validation.Outcome)
        {
            case UserValidationOutcome.NotFound:
            case UserValidationOutcome.Suspended:
                return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });
            case UserValidationOutcome.ServiceUnavailable:
                logger.LogError("User Service unavailable during reservation creation for {UserId}", userId);
                return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "An unexpected error occurred" });
        }

        if (validation.ActiveReservationsCount >= 5)
        {
            return BadRequest(new ReservationLimitExceededResponse
            {
                Error = "RESERVATION_LIMIT_EXCEEDED",
                Message = "You have reached the maximum of 5 active reservations",
                CurrentReservations = validation.ActiveReservationsCount
            });
        }

        var book = await catalogServiceClient.GetBookAsync(request.BookId);
        if (book.Outcome == BookLookupOutcome.ServiceUnavailable)
        {
            logger.LogError("Catalog Service unavailable during reservation creation for book {BookId}", request.BookId);
            return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "An unexpected error occurred" });
        }

        if (book.Outcome == BookLookupOutcome.NotFound || book.AvailableCopies <= 0)
        {
            return BadRequest(new BookUnavailableResponse
            {
                Error = "BOOK_UNAVAILABLE",
                Message = "No copies available for reservation",
                AvailableCopies = book.AvailableCopies
            });
        }

        var decremented = await catalogServiceClient.UpdateAvailabilityAsync(request.BookId, -1);
        if (!decremented)
        {
            logger.LogError("Failed to decrement availability for book {BookId}", request.BookId);
            return StatusCode(500, new ErrorResponse { Error = "INTERNAL_SERVER_ERROR", Message = "An unexpected error occurred" });
        }

        var reservedAt = DateTime.UtcNow;
        var reservation = new Reservation
        {
            BookId = request.BookId,
            UserId = userId,
            Status = ReservationStatus.Reserved,
            ReservedAt = reservedAt,
            ExpiresAt = reservedAt.AddDays(7),
            BookTitle = book.Title!,
            BookAuthor = book.Author!
        };

        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        return StatusCode(201, new ReservationResponse
        {
            ReservationId = reservation.ReservationId,
            BookId = reservation.BookId,
            UserId = reservation.UserId,
            BookTitle = reservation.BookTitle,
            Status = "RESERVED",
            ReservedAt = reservation.ReservedAt,
            ExpiresAt = reservation.ExpiresAt,
            Message = "Book reserved successfully. Please pick up within 7 days."
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetActiveReservations()
    {
        var userId = CurrentUserId;
        var now = DateTime.UtcNow;

        var reservations = await context.Reservations
            .Where(r => r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut))
            .ToListAsync();

        var items = reservations.Select(r => new ActiveReservationItem
        {
            ReservationId = r.ReservationId,
            BookId = r.BookId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            Status = r.Status == ReservationStatus.Reserved ? "RESERVED" : "CHECKED_OUT",
            ReservedAt = r.ReservedAt,
            ExpiresAt = r.ExpiresAt,
            CheckedOutAt = r.CheckedOutAt,
            DueDate = r.DueDate,
            DaysUntilExpiry = r.Status == ReservationStatus.Reserved && r.ExpiresAt.HasValue
                ? (int?)Math.Ceiling((r.ExpiresAt.Value - now).TotalDays)
                : null,
            DaysUntilDue = r.Status == ReservationStatus.CheckedOut && r.DueDate.HasValue
                ? (int?)Math.Ceiling((r.DueDate.Value - now).TotalDays)
                : null
        }).ToList();

        return Ok(new ActiveReservationsResponse
        {
            Reservations = items,
            TotalActive = items.Count
        });
    }
    
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 0, [FromQuery] int size = 20)
    {
        page = Math.Max(page, 0);
        size = Math.Clamp(size, 1, 100);

        var userId = CurrentUserId;

        var query = context.Reservations
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReturnedAt ?? r.ReservedAt);

        var totalElements = await query.CountAsync();
        var totalPages = totalElements == 0 ? 0 : (int)Math.Ceiling(totalElements / (double)size);

        var pageOfReservations = await query
            .Skip(page * size)
            .Take(size)
            .ToListAsync();

        // Projected in memory, same reasoning as GetActiveReservations above -
        // ToWireString() is a hand-written C# method with no SQL translation.
        var content = pageOfReservations.Select(r => new HistoryItemResponse
        {
            ReservationId = r.ReservationId,
            BookTitle = r.BookTitle,
            BookAuthor = r.BookAuthor,
            ReservedAt = r.ReservedAt,
            CheckedOutAt = r.CheckedOutAt,
            ReturnedAt = r.ReturnedAt,
            DueDate = r.DueDate,
            Status = r.Status.ToWireString(),
            WasLate = r.ReturnedAt.HasValue && r.DueDate.HasValue && r.ReturnedAt.Value > r.DueDate.Value
        }).ToList();

        return Ok(new PagedResult<HistoryItemResponse>
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpPost("{reservationId:guid}/checkout")]
    [Authorize(Roles = "LIBRARIAN")]
    public async Task<IActionResult> Checkout(Guid reservationId, CheckoutRequest request)
    {
        var reservation = await context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"Reservation not found with ID: {reservationId}" });
        }

        if (reservation.Status != ReservationStatus.Reserved)
        {
            return BadRequest(new InvalidStatusResponse
            {
                Error = "INVALID_STATUS",
                Message = "Can only checkout reservations with RESERVED status",
                CurrentStatus = reservation.Status.ToWireString()
            });
        }

        reservation.Status = ReservationStatus.CheckedOut;
        reservation.CheckedOutAt = DateTime.UtcNow;
        reservation.DueDate = reservation.CheckedOutAt.Value.AddDays(14);
        reservation.Notes = request.Notes;

        await context.SaveChangesAsync();

        return Ok(new CheckoutResponse
        {
            ReservationId = reservation.ReservationId,
            Status = "CHECKED_OUT",
            CheckedOutAt = reservation.CheckedOutAt.Value,
            DueDate = reservation.DueDate.Value,
            Message = $"Book checked out successfully. Due date: {reservation.DueDate.Value:MMMM d, yyyy}"
        });
    }

    [HttpPost("{reservationId:guid}/return")]
    [Authorize(Roles = "LIBRARIAN")]
    public async Task<IActionResult> ReturnBook(Guid reservationId, ReturnRequest request)
    {
        if (!Enum.TryParse<BookCondition>(request.Condition, ignoreCase: true, out var condition))
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Condition must be one of: GOOD, FAIR, POOR, DAMAGED"
            });
        }

        var reservation = await context.Reservations.FindAsync(reservationId);
        if (reservation is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"Reservation not found with ID: {reservationId}" });
        }

        if (reservation.Status != ReservationStatus.CheckedOut)
        {
            return BadRequest(new InvalidStatusResponse
            {
                Error = "INVALID_STATUS",
                Message = "Can only return books with CHECKED_OUT status",
                CurrentStatus = reservation.Status.ToString().ToUpperInvariant()
            });
        }

        var returnedAt = DateTime.UtcNow;
        var lateDays = 0;
        if (reservation.DueDate.HasValue && returnedAt > reservation.DueDate.Value)
        {
            lateDays = (int)(returnedAt.Date - reservation.DueDate.Value.Date).TotalDays;
        }
        var lateFee = lateDays * 1.00m;

        reservation.Status = ReservationStatus.Returned;
        reservation.ReturnedAt = returnedAt;
        reservation.Condition = condition;
        reservation.Notes = request.Notes;
        reservation.LateDays = lateDays;
        reservation.LateFee = lateFee;

        await waitlistCascadeService.OfferOrReleaseAsync(reservation.BookId, returnedAt);

        await context.SaveChangesAsync();

        return Ok(new ReturnResponse
        {
            ReservationId = reservation.ReservationId,
            ReturnedAt = returnedAt,
            DueDate = reservation.DueDate,
            LateDays = lateDays,
            LateFee = lateFee,
            Message = lateDays > 0
                ? $"Book returned. Late fee of ${lateFee:F2} applied to account."
                : "Book returned successfully"
        });
    }

    [HttpGet("statistics/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatistics(Guid userId)
    {
        var activeReservations = await context.Reservations
            .CountAsync(r => r.UserId == userId && (r.Status == ReservationStatus.Reserved || r.Status == ReservationStatus.CheckedOut));

        var borrowingHistory = await context.Reservations
            .CountAsync(r => r.UserId == userId && r.Status == ReservationStatus.Returned);

        return Ok(new ReservationStatisticsResponse
        {
            ActiveReservations = activeReservations,
            BorrowingHistory = borrowingHistory
        });
    }
}