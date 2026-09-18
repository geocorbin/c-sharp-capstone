using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using UserService.Controllers;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;

namespace UserService.Tests;

public class UsersControllerTests
{
    private static UserServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UserServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserServiceContext(options);
    }

    private static UsersController CreateController(UserServiceContext context, IReservationServiceClient reservationServiceClient, Guid userId)
    {
        var controller = new UsersController(context, reservationServiceClient);

        var identity = new ClaimsIdentity(new[] { new Claim("userId", userId.ToString()) }, "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        return controller;
    }

    private static async Task<User> SeedUserAsync(UserServiceContext context, MembershipStatus status = MembershipStatus.Active)
    {
        var user = new User
        {
            Email = "test@example.com",
            PasswordHash = "irrelevant-for-this-test",
            FirstName = "Test",
            LastName = "User",
            PhoneNumber = "+1-555-0123",
            MembershipStatus = status,
            MemberSince = DateTime.UtcNow
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task GetProfile_UserExists_ReturnsProfileWithStatsFromReservationService()
    {
        var context = CreateContext();
        var user = await SeedUserAsync(context);

        var reservationClient = new Mock<IReservationServiceClient>();
        reservationClient.Setup(r => r.GetStatisticsAsync(user.UserId))
            .ReturnsAsync(new ReservationStatistics(2, 15));

        var controller = CreateController(context, reservationClient.Object, user.UserId);

        var result = await controller.GetProfile();

        var ok = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(2, profile.ActiveReservations);
        Assert.Equal(15, profile.BorrowingHistory);
        Assert.Equal("PATRON", profile.Role);
    }

    [Fact]
    public async Task GetProfile_ReservationServiceUnavailable_DefaultsStatsToZeroRatherThanFailing()
    {
        var context = CreateContext();
        var user = await SeedUserAsync(context);

        var reservationClient = new Mock<IReservationServiceClient>();
        reservationClient.Setup(r => r.GetStatisticsAsync(user.UserId)).ReturnsAsync((ReservationStatistics?)null);

        var controller = CreateController(context, reservationClient.Object, user.UserId);

        var result = await controller.GetProfile();

        var ok = Assert.IsType<OkObjectResult>(result);
        var profile = Assert.IsType<ProfileResponse>(ok.Value);
        Assert.Equal(0, profile.ActiveReservations);
        Assert.Equal(0, profile.BorrowingHistory);
    }

    [Fact]
    public async Task GetProfile_UserIdFromTokenNotInDatabase_Returns401()
    {
        var context = CreateContext();
        var controller = CreateController(context, Mock.Of<IReservationServiceClient>(), Guid.NewGuid());

        var result = await controller.GetProfile();

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task ValidateUser_UserExists_Returns200WithActiveReservationsCount()
    {
        var context = CreateContext();
        var user = await SeedUserAsync(context);

        var reservationClient = new Mock<IReservationServiceClient>();
        reservationClient.Setup(r => r.GetStatisticsAsync(user.UserId))
            .ReturnsAsync(new ReservationStatistics(4, 20));

        var controller = CreateController(context, reservationClient.Object, Guid.NewGuid());

        var result = await controller.ValidateUser(user.UserId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<UserValidationResponse>(ok.Value);
        Assert.Equal(4, response.ActiveReservationsCount);
        Assert.Equal("ACTIVE", response.MembershipStatus);
    }

    [Fact]
    public async Task ValidateUser_UserDoesNotExist_Returns404()
    {
        var context = CreateContext();
        var controller = CreateController(context, Mock.Of<IReservationServiceClient>(), Guid.NewGuid());

        var result = await controller.ValidateUser(Guid.NewGuid());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ValidateUser_UserSuspended_Returns400()
    {
        var context = CreateContext();
        var user = await SeedUserAsync(context, MembershipStatus.Suspended);

        var controller = CreateController(context, Mock.Of<IReservationServiceClient>(), Guid.NewGuid());

        var result = await controller.ValidateUser(user.UserId);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("USER_SUSPENDED", error.Error);
    }
}