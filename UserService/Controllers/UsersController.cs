using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Data;
using UserService.Dtos;
using UserService.Models;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api")]
public class UsersController(UserServiceContext context, IReservationServiceClient reservationServiceClient) : ControllerBase
{
    [HttpGet("users/profile")]
    [Authorize]
    public async Task<IActionResult> GetProfile()
    {
        var userId = Guid.Parse(User.FindFirstValue("userId")!);

        var user = await context.Users.FindAsync(userId);
        if (user is null)
        {
            return Unauthorized(new ErrorResponse { Error = "UNAUTHORIZED", Message = "Authentication required" });
        }

        var stats = await reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new ProfileResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString().ToUpperInvariant(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            MemberSince = user.MemberSince,
            ActiveReservations = stats?.ActiveReservations ?? 0,
            BorrowingHistory = stats?.BorrowingHistory ?? 0
        });
    }

    [HttpGet("users/{userId:guid}/validate")]
    public async Task<IActionResult> ValidateUser(Guid userId)
    {
        var user = await context.Users.FindAsync(userId);
        if (user is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"User not found with ID: {userId}" });
        }

        if (user.MembershipStatus != MembershipStatus.Active)
        {
            return BadRequest(new ErrorResponse { Error = "USER_SUSPENDED", Message = "User account is suspended" });
        }

        var stats = await reservationServiceClient.GetStatisticsAsync(userId);

        return Ok(new UserValidationResponse
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = user.Role.ToString().ToUpperInvariant(),
            MembershipStatus = user.MembershipStatus.ToString().ToUpperInvariant(),
            ActiveReservationsCount = stats?.ActiveReservations ?? 0
        });
    }
}