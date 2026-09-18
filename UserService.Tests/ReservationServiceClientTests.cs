using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.Services;

namespace UserService.Tests;

public class ReservationServiceClientTests
{
    [Fact]
    public async Task GetStatisticsAsync_Returns200_DeserializesCorrectly()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK,
            """{"activeReservations": 2, "borrowingHistory": 10}""");
        var client = new ReservationServiceClient(FakeHttpMessageHandler.CreateClient(handler), NullLogger<ReservationServiceClient>.Instance);

        var result = await client.GetStatisticsAsync(Guid.NewGuid());

        Assert.NotNull(result);
        Assert.Equal(2, result!.ActiveReservations);
        Assert.Equal(10, result.BorrowingHistory);
    }

    [Fact]
    public async Task GetStatisticsAsync_Returns500_ReturnsNull()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.InternalServerError);
        var client = new ReservationServiceClient(FakeHttpMessageHandler.CreateClient(handler), NullLogger<ReservationServiceClient>.Instance);

        var result = await client.GetStatisticsAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetStatisticsAsync_ServiceUnreachable_ReturnsNullRatherThanThrowing()
    {
        var handler = FakeHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));
        var client = new ReservationServiceClient(FakeHttpMessageHandler.CreateClient(handler), NullLogger<ReservationServiceClient>.Instance);

        var result = await client.GetStatisticsAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}