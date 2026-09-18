using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReservationService.Services;

public enum UserValidationOutcome
{
    Success,
    NotFound,
    Suspended,
    ServiceUnavailable
}

public record UserValidationResult(UserValidationOutcome Outcome, int ActiveReservationsCount = 0);

public interface IUserServiceClient
{
    Task<UserValidationResult> ValidateUserAsync(Guid userId);
}

public class UserServiceClient(HttpClient httpClient, ILogger<UserServiceClient> logger) : IUserServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UserValidationResult> ValidateUserAsync(Guid userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/users/{userId}/validate");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UserValidationResult(UserValidationOutcome.NotFound);
            }

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                return new UserValidationResult(UserValidationOutcome.Suspended);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("User Service returned {StatusCode} validating {UserId}", response.StatusCode, userId);
                return new UserValidationResult(UserValidationOutcome.ServiceUnavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<UserValidationBody>(JsonOptions);
            return new UserValidationResult(UserValidationOutcome.Success, body?.ActiveReservationsCount ?? 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "User Service unavailable while validating {UserId}", userId);
            return new UserValidationResult(UserValidationOutcome.ServiceUnavailable);
        }
    }

    private record UserValidationBody(int ActiveReservationsCount);
}