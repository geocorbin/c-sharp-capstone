using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    UserServiceContext context,
    IPasswordHasher passwordHasher,
    IJwtTokenService jwtTokenService,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var validation = await registerValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = validation.Errors.First().ErrorMessage
            });
        }

        var emailExists = await context.Users.AnyAsync(u => u.Email == request.Email);
        if (emailExists)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = "Email already exists"
            });
        }

        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            MemberSince = DateTime.UtcNow
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        return StatusCode(201, new RegisterResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpperInvariant(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            CreatedAt = user.CreatedAt,
            Message = "Registration successful"
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var validation = await loginValidator.ValidateAsync(request);
        if (!validation.IsValid)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "VALIDATION_ERROR",
                Message = validation.Errors.First().ErrorMessage
            });
        }

        var user = await context.Users.SingleOrDefaultAsync(u => u.Email == request.Email);

        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for {Email}", request.Email);
            return Unauthorized(new ErrorResponse
            {
                Error = "AUTHENTICATION_FAILED",
                Message = "Invalid email or password"
            });
        }

        return Ok(new LoginResponse
        {
            AccessToken = jwtTokenService.GenerateToken(user),
            TokenType = "Bearer",
            ExpiresIn = jwtTokenService.ExpiresInSeconds,
            User = new UserSummary
            {
                UserId = user.UserId,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString().ToUpperInvariant()
            }
        });
    }
}