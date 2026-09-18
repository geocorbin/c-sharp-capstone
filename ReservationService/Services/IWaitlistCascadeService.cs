using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public interface IWaitlistCascadeService
{
    Task OfferOrReleaseAsync(Guid bookId, DateTime now);
}

public class WaitlistCascadeService(
    ReservationServiceContext context,
    IUserServiceClient userServiceClient,
    ICatalogServiceClient catalogServiceClient,
    IConfiguration configuration,
    ILogger<WaitlistCascadeService> logger) : IWaitlistCascadeService
{
    public async Task OfferOrReleaseAsync(Guid bookId, DateTime now)
    {
        var claimWindowMinutes = configuration.GetValue("WaitlistExpiry:ClaimWindowMinutes", 2880); // 48h default

        while (true)
        {
            var next = await context.Waitlists
                .Where(w => w.BookId == bookId && w.Status == WaitlistStatus.Waiting)
                .OrderBy(w => w.JoinedAt)
                .FirstOrDefaultAsync();

            if (next is null)
            {
                await catalogServiceClient.UpdateAvailabilityAsync(bookId, 1);
                logger.LogInformation("Book {BookId}: no eligible waitlist entry, released to general availability", bookId);
                return;
            }

            var validation = await userServiceClient.ValidateUserAsync(next.UserId);
            var eligible = validation.Outcome == UserValidationOutcome.Success && validation.ActiveReservationsCount < 5;

            if (!eligible)
            {
                next.Status = WaitlistStatus.Expired;
                await context.SaveChangesAsync();
                logger.LogInformation("Waitlist entry {WaitlistId} for book {BookId} skipped (ineligible)", next.WaitlistId, bookId);
                continue;
            }
            
            var claimDeadline = now.AddMinutes(claimWindowMinutes);
            var reservation = new Reservation
            {
                BookId = bookId,
                UserId = next.UserId,
                Status = ReservationStatus.Reserved,
                ReservedAt = now,
                ExpiresAt = claimDeadline,
                BookTitle = next.BookTitle,
                BookAuthor = next.BookAuthor
            };
            context.Reservations.Add(reservation);

            next.Status = WaitlistStatus.Notified;
            next.NotifiedAt = now;
            next.ClaimDeadline = claimDeadline;
            next.ResultingReservationId = reservation.ReservationId;

            await context.SaveChangesAsync();
            logger.LogInformation(
                "Book {BookId} offered to waitlist entry {WaitlistId} - reservation {ReservationId} created, claim deadline {ClaimDeadline}",
                bookId, next.WaitlistId, reservation.ReservationId, claimDeadline);
            return;
        }
    }
}