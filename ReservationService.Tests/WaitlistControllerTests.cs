using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservationService.Controllers;
using ReservationService.Data;
using ReservationService.Dtos;
using ReservationService.Models;
using ReservationService.Services;

namespace ReservationService.Tests;

public class WaitlistControllerTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }

    private static WaitlistController CreateController(
        ReservationServiceContext context,
        ICatalogServiceClient catalogServiceClient,
        IWaitlistCascadeService waitlistCascadeService,
        Guid userId)
    {
        var controller = new WaitlistController(context, catalogServiceClient, waitlistCascadeService, NullLogger<WaitlistController>.Instance);

        var identity = new ClaimsIdentity(new[] { new Claim("userId", userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    [Fact]
    public async Task JoinWaitlist_BookDoesNotExist_Returns404()
    {
        var context = CreateContext();
        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.NotFound));

        var controller = CreateController(context, catalogClient.Object, Mock.Of<IWaitlistCascadeService>(), Guid.NewGuid());

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task JoinWaitlist_CatalogServiceUnavailable_Returns500()
    {
        var context = CreateContext();
        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.ServiceUnavailable));

        var controller = CreateController(context, catalogClient.Object, Mock.Of<IWaitlistCascadeService>(), Guid.NewGuid());

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task JoinWaitlist_BookHasAvailableCopies_Returns400BookAvailable()
    {
        var context = CreateContext();
        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.Found, "Clean Code", "Robert C. Martin", AvailableCopies: 2));

        var controller = CreateController(context, catalogClient.Object, Mock.Of<IWaitlistCascadeService>(), Guid.NewGuid());

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = Guid.NewGuid() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("BOOK_AVAILABLE", error.Error);
    }

    [Fact]
    public async Task JoinWaitlist_AlreadyWaitingForSameBook_Returns400AlreadyWaitlisted()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        context.Waitlists.Add(new Waitlist { BookId = bookId, UserId = userId, Status = WaitlistStatus.Waiting, JoinedAt = DateTime.UtcNow, BookTitle = "T", BookAuthor = "A" });
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.Found, "T", "A", AvailableCopies: 0));

        var controller = CreateController(context, catalogClient.Object, Mock.Of<IWaitlistCascadeService>(), userId);

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = bookId });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("ALREADY_WAITLISTED", error.Error);
    }

    [Fact]
    public async Task JoinWaitlist_TwoAlreadyWaiting_NewEntryGetsPositionThree()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Waitlists.AddRange(
            new Waitlist { BookId = bookId, UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = now.AddMinutes(-20), BookTitle = "T", BookAuthor = "A" },
            new Waitlist { BookId = bookId, UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = now.AddMinutes(-10), BookTitle = "T", BookAuthor = "A" }
        );
        await context.SaveChangesAsync();

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.Found, "T", "A", AvailableCopies: 0));

        var controller = CreateController(context, catalogClient.Object, Mock.Of<IWaitlistCascadeService>(), Guid.NewGuid());

        var result = await controller.JoinWaitlist(new JoinWaitlistRequest { BookId = bookId });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<WaitlistJoinResponse>(objectResult.Value);
        Assert.Equal(3, response.Position);
        Assert.Equal("WAITING", response.Status);
    }

    [Fact]
    public async Task GetMyWaitlist_MixOfStatuses_ReturnsOnlyWaitingAndNotifiedWithCorrectFields()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Waitlists.AddRange(
            new Waitlist { BookId = Guid.NewGuid(), UserId = userId, Status = WaitlistStatus.Waiting, JoinedAt = now, BookTitle = "Waiting Book", BookAuthor = "A" },
            new Waitlist { BookId = Guid.NewGuid(), UserId = userId, Status = WaitlistStatus.Notified, JoinedAt = now.AddDays(-1), NotifiedAt = now, ClaimDeadline = now.AddHours(48), BookTitle = "Notified Book", BookAuthor = "B" },
            new Waitlist { BookId = Guid.NewGuid(), UserId = userId, Status = WaitlistStatus.Cancelled, JoinedAt = now.AddDays(-5), BookTitle = "Cancelled Book", BookAuthor = "C" },
            new Waitlist { BookId = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = now, BookTitle = "Someone Else's", BookAuthor = "D" }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<ICatalogServiceClient>(), Mock.Of<IWaitlistCascadeService>(), userId);

        var result = await controller.GetMyWaitlist();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<WaitlistListResponse>(ok.Value);

        Assert.Equal(2, response.Entries.Count);
        Assert.DoesNotContain(response.Entries, e => e.BookTitle is "Cancelled Book" or "Someone Else's");

        var waiting = response.Entries.Single(e => e.BookTitle == "Waiting Book");
        Assert.Equal(1, waiting.Position);
        Assert.Null(waiting.ClaimDeadline);

        var notified = response.Entries.Single(e => e.BookTitle == "Notified Book");
        Assert.Null(notified.Position);
        Assert.NotNull(notified.ClaimDeadline);
    }

    [Fact]
    public async Task LeaveWaitlist_EntryBelongsToSomeoneElse_Returns404()
    {
        var context = CreateContext();
        var entry = new Waitlist { BookId = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = WaitlistStatus.Waiting, JoinedAt = DateTime.UtcNow, BookTitle = "T", BookAuthor = "A" };
        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<ICatalogServiceClient>(), Mock.Of<IWaitlistCascadeService>(), Guid.NewGuid());

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task LeaveWaitlist_EntryWasWaiting_CancelsWithoutInvokingCascade()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var entry = new Waitlist { BookId = Guid.NewGuid(), UserId = userId, Status = WaitlistStatus.Waiting, JoinedAt = DateTime.UtcNow, BookTitle = "T", BookAuthor = "A" };
        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(context, Mock.Of<ICatalogServiceClient>(), cascadeService.Object, userId);

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<OkObjectResult>(result);
        cascadeService.Verify(c => c.OfferOrReleaseAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()), Times.Never);

        var updated = await context.Waitlists.FindAsync(entry.WaitlistId);
        Assert.Equal(WaitlistStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task LeaveWaitlist_EntryWasNotified_CancelsAndInvokesCascadeForThatBook()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();
        var entry = new Waitlist { BookId = bookId, UserId = userId, Status = WaitlistStatus.Notified, JoinedAt = DateTime.UtcNow.AddDays(-1), NotifiedAt = DateTime.UtcNow, ClaimDeadline = DateTime.UtcNow.AddHours(48), BookTitle = "T", BookAuthor = "A" };
        context.Waitlists.Add(entry);
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(context, Mock.Of<ICatalogServiceClient>(), cascadeService.Object, userId);

        var result = await controller.LeaveWaitlist(entry.WaitlistId);

        Assert.IsType<OkObjectResult>(result);
        cascadeService.Verify(c => c.OfferOrReleaseAsync(bookId, It.IsAny<DateTime>()), Times.Once);
    }
}