using System.Net.Http.Json;
using System.Text.Json;

namespace UserService.Services;

public record ReservationStatistics(int ActiveReservations, int BorrowingHistory);

public interface IReservationServiceClient
{
    Task<ReservationStatistics?> GetStatisticsAsync(Guid userId);
}

public class ReservationServiceClient(HttpClient httpClient, ILogger<ReservationServiceClient> logger) : IReservationServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReservationStatistics?> GetStatisticsAsync(Guid userId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/reservations/statistics/{userId}");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Reservation Service returned {StatusCode} for statistics on {UserId}",
                    response.StatusCode, userId);
                return null;
            }

            return await response.Content.ReadFromJsonAsync<ReservationStatistics>(JsonOptions);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Reservation Service unavailable while fetching statistics for {UserId}", userId);
            return null;
        }
    }
}