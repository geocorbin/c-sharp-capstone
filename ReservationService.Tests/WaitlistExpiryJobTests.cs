using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Tests;

public class WaitlistExpiryJobTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }
    private static IServiceScopeFactory CreateScopeFactory(ReservationServiceContext context, IWaitlistCascadeService cascadeService)
    {
        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton(cascadeService);
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static WaitlistExpiryJob CreateJob(ReservationServiceContext context, IWaitlistCascadeService cascadeService) =>
        new(CreateScopeFactory(context, cascadeService), new ConfigurationBuilder().Build(), NullLogger<WaitlistExpiryJob>.Instance);

    [Fact]
    public async Task ProcessExpiredClaimsAsync_NoNotifiedEntriesPastDeadline_CascadeNeverInvoked()
    {
        var context = CreateContext();
        var cascadeService = new Mock<IWaitlistCascadeService>();

        var job = CreateJob(context, cascadeService.Object);

        await job.ProcessExpiredClaimsAsync(CancellationToken.None);

        cascadeService.Verify(c => c.OfferOrReleaseAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredClaimsAsync_ClaimDeadlineStillInFuture_LeftUntouched()
    {
        var context = CreateContext();
        var entry = new Waitlist
        {
            BookId = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = WaitlistStatus.Notified,
            JoinedAt = DateTime.UtcNow.AddDays(-1), NotifiedAt = DateTime.UtcNow,
            ClaimDeadline = DateTime.UtcNow.AddHours(47), BookTitle = "T", BookAuthor = "A"
        };
        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var job = CreateJob(context, cascadeService.Object);

        await job.ProcessExpiredClaimsAsync(CancellationToken.None);

        var unchanged = await context.Waitlists.FindAsync(entry.WaitlistId);
        Assert.Equal(WaitlistStatus.Notified, unchanged!.Status);
        cascadeService.Verify(c => c.OfferOrReleaseAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredClaimsAsync_ExpiredClaimWithStillReservedResultingReservation_ExpiresEntryAndCancelsReservation()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();

        var reservation = new Reservation
        {
            BookId = bookId, UserId = Guid.NewGuid(), Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow.AddHours(-49), BookTitle = "T", BookAuthor = "A"
        };
        context.Reservations.Add(reservation);

        var entry = new Waitlist
        {
            BookId = bookId, UserId = reservation.UserId, Status = WaitlistStatus.Notified,
            JoinedAt = DateTime.UtcNow.AddDays(-3), NotifiedAt = DateTime.UtcNow.AddHours(-49),
            ClaimDeadline = DateTime.UtcNow.AddHours(-1), ResultingReservationId = reservation.ReservationId,
            BookTitle = "T", BookAuthor = "A"
        };
        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var job = CreateJob(context, cascadeService.Object);

        await job.ProcessExpiredClaimsAsync(CancellationToken.None);

        var updatedEntry = await context.Waitlists.FindAsync(entry.WaitlistId);
        Assert.Equal(WaitlistStatus.Expired, updatedEntry!.Status);

        var updatedReservation = await context.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Cancelled, updatedReservation!.Status);

        cascadeService.Verify(c => c.OfferOrReleaseAsync(bookId, It.IsAny<DateTime>()), Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredClaimsAsync_TwoExpiredClaimsForDifferentBooks_CascadesEachBookExactlyOnce()
    {
        var context = CreateContext();
        var bookIdA = Guid.NewGuid();
        var bookIdB = Guid.NewGuid();
        var pastDeadline = DateTime.UtcNow.AddHours(-1);

        context.Waitlists.AddRange(
            new Waitlist { BookId = bookIdA, UserId = Guid.NewGuid(), Status = WaitlistStatus.Notified, JoinedAt = DateTime.UtcNow.AddDays(-2), NotifiedAt = DateTime.UtcNow.AddDays(-2), ClaimDeadline = pastDeadline, BookTitle = "A", BookAuthor = "AA" },
            new Waitlist { BookId = bookIdB, UserId = Guid.NewGuid(), Status = WaitlistStatus.Notified, JoinedAt = DateTime.UtcNow.AddDays(-2), NotifiedAt = DateTime.UtcNow.AddDays(-2), ClaimDeadline = pastDeadline, BookTitle = "B", BookAuthor = "BB" }
        );
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var job = CreateJob(context, cascadeService.Object);

        await job.ProcessExpiredClaimsAsync(CancellationToken.None);

        cascadeService.Verify(c => c.OfferOrReleaseAsync(bookIdA, It.IsAny<DateTime>()), Times.Once);
        cascadeService.Verify(c => c.OfferOrReleaseAsync(bookIdB, It.IsAny<DateTime>()), Times.Once);
    }
}