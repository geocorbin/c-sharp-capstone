using System.Net;
using System.Text;

namespace UserService.Tests;

internal class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode? _statusCode;
    private readonly string? _jsonBody;
    private readonly Exception? _exceptionToThrow;

    private FakeHttpMessageHandler(HttpStatusCode? statusCode, string? jsonBody, Exception? exceptionToThrow)
    {
        _statusCode = statusCode;
        _jsonBody = jsonBody;
        _exceptionToThrow = exceptionToThrow;
    }

    public static FakeHttpMessageHandler ReturningStatus(HttpStatusCode statusCode, string? jsonBody = null) =>
        new(statusCode, jsonBody, null);

    public static FakeHttpMessageHandler Throwing(Exception exception) => new(null, null, exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        var response = new HttpResponseMessage(_statusCode!.Value);
        if (_jsonBody is not null)
        {
            response.Content = new StringContent(_jsonBody, Encoding.UTF8, "application/json");
        }
        return Task.FromResult(response);
    }

    public static HttpClient CreateClient(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://test.local") };
}