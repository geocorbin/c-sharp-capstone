namespace ReservationService.Dtos;

public class WaitlistCancelResponse
{
    public required Guid WaitlistId { get; set; }
    public required string Status { get; set; }
    public required string Message { get; set; }
}