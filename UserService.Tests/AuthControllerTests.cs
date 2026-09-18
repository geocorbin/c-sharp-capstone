using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.Controllers;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;
using UserService.Validators;

namespace UserService.Tests;

public class AuthControllerTests
{
    private static UserServiceContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<UserServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new UserServiceContext(options);
    }

    private static AuthController CreateController(UserServiceContext context) =>
        new(
            context,
            new BCryptPasswordHasher(),
            new JwtTokenServiceStub(),
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            NullLogger<AuthController>.Instance);

    private class JwtTokenServiceStub : IJwtTokenService
    {
        public int ExpiresInSeconds => 86400;
        public string GenerateToken(User user) => "fake-token";
    }

    [Fact]
    public async Task Register_NewEmail_Returns201WithPatronDefaults()
    {
        var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Register(new RegisterRequest
        {
            Email = "new@example.com",
            Password = "Test123!@#",
            FirstName = "New",
            LastName = "User",
            PhoneNumber = "+1-555-0123"
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(201, objectResult.StatusCode);

        var response = Assert.IsType<RegisterResponse>(objectResult.Value);
        Assert.Equal("PATRON", response.Role);
        Assert.Equal("ACTIVE", response.MembershipStatus);
    }

    [Fact]
    public async Task Register_EmailAlreadyExists_Returns400()
    {
        var context = CreateContext();
        context.Users.Add(new User
        {
            Email = "taken@example.com",
            PasswordHash = "irrelevant-for-this-test",
            FirstName = "Existing",
            LastName = "User",
            PhoneNumber = "+1-555-0000"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        var result = await controller.Register(new RegisterRequest
        {
            Email = "taken@example.com",
            Password = "Test123!@#",
            FirstName = "New",
            LastName = "User",
            PhoneNumber = "+1-555-0123"
        });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Error);
    }

    [Fact]
    public async Task Login_CorrectCredentials_ReturnsTokenAndUserSummary()
    {
        var context = CreateContext();
        var hasher = new BCryptPasswordHasher();
        context.Users.Add(new User
        {
            Email = "login@example.com",
            PasswordHash = hasher.Hash("Test123!@#"),
            FirstName = "Login",
            LastName = "Test",
            PhoneNumber = "+1-555-0123"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        var result = await controller.Login(new LoginRequest { Email = "login@example.com", Password = "Test123!@#" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<LoginResponse>(ok.Value);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(86400, response.ExpiresIn);
        Assert.Equal("login@example.com", response.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var context = CreateContext();
        var hasher = new BCryptPasswordHasher();
        context.Users.Add(new User
        {
            Email = "login@example.com",
            PasswordHash = hasher.Hash("Test123!@#"),
            FirstName = "Login",
            LastName = "Test",
            PhoneNumber = "+1-555-0123"
        });
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        var result = await controller.Login(new LoginRequest { Email = "login@example.com", Password = "WrongPassword1!" });

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(unauthorized.Value);
        Assert.Equal("AUTHENTICATION_FAILED", error.Error);
    }

    [Fact]
    public async Task Login_EmailNotRegistered_Returns401()
    {
        var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Login(new LoginRequest { Email = "nobody@example.com", Password = "Test123!@#" });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Login_EmptyEmailAndPassword_Returns400BeforeTouchingDatabase()
    {
        var context = CreateContext();
        var controller = CreateController(context);

        var result = await controller.Login(new LoginRequest { Email = "", Password = "" });

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(badRequest.Value);
        Assert.Equal("VALIDATION_ERROR", error.Error);
    }
}