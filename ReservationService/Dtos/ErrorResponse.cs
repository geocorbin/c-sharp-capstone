namespace ReservationService.Dtos;

public class ErrorResponse
{
    public required string Error { get; set; }
    public required string Message { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}