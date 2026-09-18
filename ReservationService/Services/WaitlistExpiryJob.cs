using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Models;

namespace ReservationService.Services;

public class WaitlistExpiryJob(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<WaitlistExpiryJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("WaitlistExpiry:IntervalMinutes", 60);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredClaimsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Waitlist expiry job failed on this pass - will retry next interval");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    public async Task ProcessExpiredClaimsAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationServiceContext>();
        var cascadeService = scope.ServiceProvider.GetRequiredService<IWaitlistCascadeService>();

        var now = DateTime.UtcNow;

        var expired = await context.Waitlists
            .Where(w => w.Status == WaitlistStatus.Notified && w.ClaimDeadline != null && w.ClaimDeadline < now)
            .ToListAsync(stoppingToken);

        if (expired.Count == 0)
        {
            logger.LogInformation("Waitlist expiry job: no expired claims found");
            return;
        }

        logger.LogInformation("Waitlist expiry job: found {Count} expired claim(s)", expired.Count);

        foreach (var entry in expired)
        {
            entry.Status = WaitlistStatus.Expired;

            if (entry.ResultingReservationId.HasValue)
            {
                var reservation = await context.Reservations.FindAsync([entry.ResultingReservationId.Value], stoppingToken);
                if (reservation is not null && reservation.Status == ReservationStatus.Reserved)
                {
                    reservation.Status = ReservationStatus.Cancelled;
                }
            }
        }
        await context.SaveChangesAsync(stoppingToken);

        foreach (var bookId in expired.Select(w => w.BookId).Distinct())
        {
            await cascadeService.OfferOrReleaseAsync(bookId, now);
            logger.LogInformation("Waitlist expiry job: cascaded book {BookId}", bookId);
        }
    }
}