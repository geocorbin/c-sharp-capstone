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

public class ReservationsControllerTests
{
    private static ReservationServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ReservationServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ReservationServiceContext(options);
    }

    private static ReservationsController CreateController(
        ReservationServiceContext context,
        IUserServiceClient userServiceClient,
        ICatalogServiceClient catalogServiceClient,
        IWaitlistCascadeService waitlistCascadeService,
        Guid userId)
    {
        var controller = new ReservationsController(
            context, userServiceClient, catalogServiceClient,
            waitlistCascadeService, NullLogger<ReservationsController>.Instance);

        var identity = new ClaimsIdentity(new[] { new Claim("userId", userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    private static ReservationsController CreateController(
        ReservationServiceContext context,
        IUserServiceClient userServiceClient,
        ICatalogServiceClient catalogServiceClient,
        Guid userId) =>
        CreateController(context, userServiceClient, catalogServiceClient, Mock.Of<IWaitlistCascadeService>(), userId);

    [Fact]
    public async Task CreateReservation_UserUnderLimitAndBookAvailable_Returns201AndDecrementsAvailability()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 2));

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.Found, "Clean Code", "Robert C. Martin", AvailableCopies: 3));
        catalogClient.Setup(c => c.UpdateAvailabilityAsync(bookId, -1)).ReturnsAsync(true);

        var controller = CreateController(context, userClient.Object, catalogClient.Object, userId);

        var result = await controller.CreateReservation(new CreateReservationRequest { BookId = bookId });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);
        var response = Assert.IsType<ReservationResponse>(objectResult.Value);
        Assert.Equal("RESERVED", response.Status);
        Assert.Equal("Clean Code", response.BookTitle);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(bookId, -1), Times.Once);
        Assert.Single(context.Reservations);
    }

    [Fact]
    public async Task CreateReservation_UserAtFiveActiveReservations_Returns400WithoutCheckingCatalog()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 5));

        var catalogClient = new Mock<ICatalogServiceClient>();

        var controller = CreateController(context, userClient.Object, catalogClient.Object, userId);

        var result = await controller.CreateReservation(new CreateReservationRequest { BookId = Guid.NewGuid() });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ReservationLimitExceededResponse>(badRequest.Value);
        Assert.Equal("RESERVATION_LIMIT_EXCEEDED", error.Error);

        catalogClient.Verify(c => c.GetBookAsync(It.IsAny<Guid>()), Times.Never);
        Assert.Empty(context.Reservations);
    }

    [Fact]
    public async Task CreateReservation_BookHasNoAvailableCopies_Returns400WithoutDecrementing()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var bookId = Guid.NewGuid();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.Success, ActiveReservationsCount: 0));

        var catalogClient = new Mock<ICatalogServiceClient>();
        catalogClient.Setup(c => c.GetBookAsync(bookId))
            .ReturnsAsync(new BookLookupResult(BookLookupOutcome.Found, "Refactoring", "Martin Fowler", AvailableCopies: 0));

        var controller = CreateController(context, userClient.Object, catalogClient.Object, userId);

        var result = await controller.CreateReservation(new CreateReservationRequest { BookId = bookId });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<BookUnavailableResponse>(badRequest.Value);
        Assert.Equal("BOOK_UNAVAILABLE", error.Error);

        catalogClient.Verify(c => c.UpdateAvailabilityAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task CreateReservation_UserServiceUnreachable_Returns500()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        var userClient = new Mock<IUserServiceClient>();
        userClient.Setup(u => u.ValidateUserAsync(userId))
            .ReturnsAsync(new UserValidationResult(UserValidationOutcome.ServiceUnavailable));

        var controller = CreateController(context, userClient.Object, Mock.Of<ICatalogServiceClient>(), userId);

        var result = await controller.CreateReservation(new CreateReservationRequest { BookId = Guid.NewGuid() });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetActiveReservations_MixOfStatuses_ReturnsOnlyReservedAndCheckedOut()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Reservations.AddRange(
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Reserved, ReservedAt = now, ExpiresAt = now.AddDays(7), BookTitle = "A", BookAuthor = "AA" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.CheckedOut, ReservedAt = now.AddDays(-3), CheckedOutAt = now.AddDays(-2), DueDate = now.AddDays(12), BookTitle = "B", BookAuthor = "BB" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Returned, ReservedAt = now.AddDays(-20), ReturnedAt = now.AddDays(-5), BookTitle = "C", BookAuthor = "CC" }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), userId);

        var result = await controller.GetActiveReservations();

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ActiveReservationsResponse>(ok.Value);
        Assert.Equal(2, response.TotalActive);
        Assert.DoesNotContain(response.Reservations, r => r.BookTitle == "C");

        var reserved = response.Reservations.Single(r => r.BookTitle == "A");
        Assert.Equal(7, reserved.DaysUntilExpiry);
        Assert.Null(reserved.DaysUntilDue);

        var checkedOut = response.Reservations.Single(r => r.BookTitle == "B");
        Assert.Equal(12, checkedOut.DaysUntilDue);
        Assert.Null(checkedOut.DaysUntilExpiry);
    }

    [Fact]
    public async Task Checkout_ReservationIsReserved_SetsCheckedOutAndDueDate14DaysOut()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "Clean Code",
            BookAuthor = "Robert C. Martin"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), reservation.UserId);

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest { Notes = "Good condition" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<CheckoutResponse>(ok.Value);
        Assert.Equal("CHECKED_OUT", response.Status);

        var updated = await context.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.CheckedOut, updated!.Status);
        Assert.Equal(updated.CheckedOutAt!.Value.AddDays(14), updated.DueDate);
        Assert.Equal("Good condition", updated.Notes);
    }

    [Fact]
    public async Task Checkout_ReservationAlreadyCheckedOut_Returns400WithCorrectlyFormattedStatus()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-1),
            CheckedOutAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), reservation.UserId);

        var result = await controller.Checkout(reservation.ReservationId, new CheckoutRequest());

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<InvalidStatusResponse>(badRequest.Value);
        Assert.Equal("INVALID_STATUS", error.Error);
        Assert.Equal("CHECKED_OUT", error.CurrentStatus);
    }

    [Fact]
    public async Task Checkout_ReservationDoesNotExist_Returns404()
    {
        var context = CreateContext();
        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), Guid.NewGuid());

        var result = await controller.Checkout(Guid.NewGuid(), new CheckoutRequest());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReturnBook_ReturnedOnTime_NoFeeAndCascadeServiceInvoked()
    {
        var context = CreateContext();
        var bookId = Guid.NewGuid();
        var reservation = new Reservation
        {
            BookId = bookId,
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-10),
            CheckedOutAt = DateTime.UtcNow.AddDays(-5),
            DueDate = DateTime.UtcNow.AddDays(9),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var cascadeService = new Mock<IWaitlistCascadeService>();
        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), cascadeService.Object, reservation.UserId);

        var result = await controller.ReturnBook(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(ok.Value);
        Assert.Equal(0, response.LateDays);
        Assert.Equal(0m, response.LateFee);

        cascadeService.Verify(c => c.OfferOrReleaseAsync(bookId, It.IsAny<DateTime>()), Times.Once);

        var updated = await context.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Returned, updated!.Status);
        Assert.Equal(BookCondition.Good, updated.Condition);
    }

    [Fact]
    public async Task ReturnBook_ReturnedLate_CalculatesLateDaysAndFee()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow.AddDays(-20),
            CheckedOutAt = DateTime.UtcNow.AddDays(-17),
            DueDate = DateTime.UtcNow.AddDays(-3),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), Mock.Of<IWaitlistCascadeService>(), reservation.UserId);

        var result = await controller.ReturnBook(reservation.ReservationId, new ReturnRequest { Condition = "FAIR" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReturnResponse>(ok.Value);
        Assert.Equal(3, response.LateDays);
        Assert.Equal(3.00m, response.LateFee);
        Assert.Contains("Late fee", response.Message);
    }

    [Fact]
    public async Task ReturnBook_InvalidConditionString_Returns400BeforeTouchingReservation()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.CheckedOut,
            ReservedAt = DateTime.UtcNow,
            CheckedOutAt = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(14),
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), reservation.UserId);

        var result = await controller.ReturnBook(reservation.ReservationId, new ReturnRequest { Condition = "PRETTY_OKAY_I_GUESS" });

        Assert.IsType<BadRequestObjectResult>(result);

        var untouched = await context.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.CheckedOut, untouched!.Status);
    }

    [Fact]
    public async Task ReturnBook_ReservationNotCheckedOut_Returns400()
    {
        var context = CreateContext();
        var reservation = new Reservation
        {
            BookId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Status = ReservationStatus.Reserved,
            ReservedAt = DateTime.UtcNow,
            BookTitle = "T",
            BookAuthor = "A"
        };
        context.Reservations.Add(reservation);
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), reservation.UserId);

        var result = await controller.ReturnBook(reservation.ReservationId, new ReturnRequest { Condition = "GOOD" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<InvalidStatusResponse>(badRequest.Value);
        Assert.Equal("RESERVED", error.CurrentStatus);
    }

    [Fact]
    public async Task GetHistory_MultipleStatuses_ReturnsAllSortedNewestFirstWithCorrectStatusFormatting()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        context.Reservations.AddRange(
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Returned, ReservedAt = now.AddDays(-30), ReturnedAt = now.AddDays(-25), DueDate = now.AddDays(-26), BookTitle = "Old Return", BookAuthor = "A" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.CheckedOut, ReservedAt = now.AddDays(-2), CheckedOutAt = now.AddDays(-1), DueDate = now.AddDays(13), BookTitle = "Currently Out", BookAuthor = "B" },
            new Reservation { BookId = Guid.NewGuid(), UserId = Guid.NewGuid(), Status = ReservationStatus.Returned, ReservedAt = now, ReturnedAt = now, BookTitle = "Someone Else's", BookAuthor = "C" }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), userId);

        var result = await controller.GetHistory();

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<HistoryItemResponse>>(ok.Value);

        Assert.Equal(2, page.TotalElements);
        Assert.DoesNotContain(page.Content, h => h.BookTitle == "Someone Else's");
        Assert.Equal("Currently Out", page.Content[0].BookTitle);

        var checkedOutItem = page.Content.Single(h => h.BookTitle == "Currently Out");
        Assert.Equal("CHECKED_OUT", checkedOutItem.Status);
    }

    [Fact]
    public async Task GetStatistics_CountsActiveAndReturnedSeparately()
    {
        var context = CreateContext();
        var userId = Guid.NewGuid();

        context.Reservations.AddRange(
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Reserved, ReservedAt = DateTime.UtcNow, BookTitle = "A", BookAuthor = "AA" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.CheckedOut, ReservedAt = DateTime.UtcNow, BookTitle = "B", BookAuthor = "BB" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Returned, ReservedAt = DateTime.UtcNow, BookTitle = "C", BookAuthor = "CC" },
            new Reservation { BookId = Guid.NewGuid(), UserId = userId, Status = ReservationStatus.Cancelled, ReservedAt = DateTime.UtcNow, BookTitle = "D", BookAuthor = "DD" }
        );
        await context.SaveChangesAsync();

        var controller = CreateController(context, Mock.Of<IUserServiceClient>(), Mock.Of<ICatalogServiceClient>(), Guid.NewGuid());

        var result = await controller.GetStatistics(userId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ReservationStatisticsResponse>(ok.Value);
        Assert.Equal(2, response.ActiveReservations);
        Assert.Equal(1, response.BorrowingHistory);
    }
}