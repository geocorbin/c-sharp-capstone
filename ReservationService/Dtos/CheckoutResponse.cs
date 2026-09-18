namespace ReservationService.Dtos;

public class CheckoutResponse
{
    public required Guid ReservationId { get; set; }
    public required string Status { get; set; }
    public DateTime CheckedOutAt { get; set; }
    public DateTime DueDate { get; set; }
    public required string Message { get; set; }
}
