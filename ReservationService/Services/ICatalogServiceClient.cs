using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReservationService.Services;

public enum BookLookupOutcome
{
    Found,
    NotFound,
    ServiceUnavailable
}

public record BookLookupResult(BookLookupOutcome Outcome, string? Title = null, string? Author = null, int AvailableCopies = 0);

public interface ICatalogServiceClient
{
    Task<BookLookupResult> GetBookAsync(Guid bookId);
    Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta);
}

public class CatalogServiceClient(HttpClient httpClient, ILogger<CatalogServiceClient> logger) : ICatalogServiceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BookLookupResult> GetBookAsync(Guid bookId)
    {
        try
        {
            var response = await httpClient.GetAsync($"/api/catalog/books/{bookId}");

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new BookLookupResult(BookLookupOutcome.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Catalog Service returned {StatusCode} fetching {BookId}", response.StatusCode, bookId);
                return new BookLookupResult(BookLookupOutcome.ServiceUnavailable);
            }

            var body = await response.Content.ReadFromJsonAsync<BookLookupBody>(JsonOptions);
            if (body is null)
            {
                return new BookLookupResult(BookLookupOutcome.ServiceUnavailable);
            }

            return new BookLookupResult(BookLookupOutcome.Found, body.Title, body.Author, body.AvailableCopies);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Catalog Service unavailable while fetching {BookId}", bookId);
            return new BookLookupResult(BookLookupOutcome.ServiceUnavailable);
        }
    }

    public async Task<bool> UpdateAvailabilityAsync(Guid bookId, int delta)
    {
        try
        {
            var response = await httpClient.PutAsJsonAsync($"/api/catalog/books/{bookId}/availability", new { delta });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Catalog Service unavailable while updating availability for {BookId}", bookId);
            return false;
        }
    }

    private record BookLookupBody(string Title, string Author, int AvailableCopies);
}