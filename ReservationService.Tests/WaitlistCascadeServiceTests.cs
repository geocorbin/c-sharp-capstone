using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservationService.Data;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Tests;

public class WaitlistCascadeServiceTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }

    private static IConfiguration CreateConfiguration(int claimWindowMinutes = 2880)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WaitlistExpiry:ClaimWindowMinutes"] = claimWindowMinutes.ToString()
            })
            .Build();
    }

    [Fact]
    public async Task OfferOrReleaseAsync_NoWaitlistEntries_ReleasesCopyToGeneralAvailability()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var service = new WaitlistCascadeService(
            context,
            Mock.Of<IUserServiceClient>(),
            catalogClient.Object,
            CreateConfiguration(),
            NullLogger<WaitlistCascadeService>.Instance);

        await service.OfferOrReleaseAsync(bookId, DateTime.UtcNow);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, 1), Times.Once);
    }

    [Fact]
    public async Task OfferOrReleaseAsync_OneEligibleWaiter_CreatesReservationAndNotifiesInsteadOfReleasing()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        context.Waitlists.Add(new Waitlist
        {
            BookId = bookId,
            UserId = userId,
            Status = WaitlistStatus.Waiting,
            JoinedAt = now.AddDays(-1),
            BookTitle = "Test Book",
            BookAuthor = "Test Author"
        });
        await context.SaveChangesAsync();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 2));

        var catalogClient = new Mock<ICatalogServiceClient>();

        var service = new WaitlistCascadeService(
            context, userClient.Object, catalogClient.Object,
            CreateConfiguration(claimWindowMinutes: 60), NullLogger<WaitlistCascadeService>.Instance);

        await service.OfferOrReleaseAsync(bookId, now);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);

        var entry = await context.Waitlists.SingleAsync();
        Assert.Equal(WaitlistStatus.Notified, entry.Status);
        Assert.Equal(now, entry.NotifiedAt);
        Assert.Equal(now.AddMinutes(60), entry.ClaimDeadline);
        Assert.NotNull(entry.ResultingReservationId);

        var reservation = await context.Reservations.SingleAsync();
        Assert.Equal(entry.ResultingReservationId, reservation.ReservationId);
        Assert.Equal(userId, reservation.UserId);
        Assert.Equal(ReservationStatus.Reserved, reservation.Status);
        Assert.Equal(now.AddMinutes(60), reservation.ExpiresAt);
    }

    [Fact]
    public async Task OfferOrReleaseAsync_FirstWaiterOverLimit_SkipsToNextEligibleWaiter()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var ineligibleUserId = Guid.NewGuid();
        var eligibleUserId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Waitlists.AddRange(
            new Waitlist { BookId = bookId, UserId = ineligibleUserId, Status = WaitlistStatus.Waiting, JoinedAt = now.AddDays(-2), BookTitle = "T", BookAuthor = "A" },
            new Waitlist { BookId = bookId, UserId = eligibleUserId, Status = WaitlistStatus.Waiting, JoinedAt = now.AddDays(-1), BookTitle = "T", BookAuthor = "A" }
        );
        await context.SaveChangesAsync();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(ineligibleUserId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 5));
        userClient.Setup(u => u.ValidateUserAsync(eligibleUserId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 1));

        var service = new WaitlistCascadeService(
            context, userClient.Object, Mock.Of<ICatalogServiceClient>(),
            CreateConfiguration(), NullLogger<WaitlistCascadeService>.Instance);

        await service.OfferOrReleaseAsync(bookId, now);

        var ineligibleEntry = await context.Waitlists.SingleAsync(w => w.UserId == ineligibleUserId);
        Assert.Equal(WaitlistStatus.Expired, ineligibleEntry.Status);

        var eligibleEntry = await context.Waitlists.SingleAsync(w => w.UserId == eligibleUserId);
        Assert.Equal(WaitlistStatus.Notified, eligibleEntry.Status);
    }

    [Fact]
    public async Task OfferOrReleaseAsync_EveryoneWaitingOverLimit_ExpiresAllAndReleasesCopy()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Waitlists.Add(new Waitlist
        {
            BookId = bookId, UserId = userId, Status = WaitlistStatus.Waiting, JoinedAt = now, BookTitle = "T", BookAuthor = "A"
        });
        await context.SaveChangesAsync();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 5));

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, 1)).ReturnsAsync(true);

        var service = new WaitlistCascadeService(
            context, userClient.Object, catalogClient.Object,
            CreateConfiguration(), NullLogger<WaitlistCascadeService>.Instance);

        await service.OfferOrReleaseAsync(bookId, now);

        var entry = await context.Waitlists.SingleAsync();
        Assert.Equal(WaitlistStatus.Expired, entry.Status);
        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, 1), Times.Once);
        Assert.Empty(context.Reservations);
    }
}