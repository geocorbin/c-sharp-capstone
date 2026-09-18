namespace ReservationService.Dtos;

public class ReturnResponse
{
    public required Guid ReservationId { get; set; }
    public DateTime ReturnedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int LateDays { get; set; }
    public decimal LateFee { get; set; }
    public required string Message { get; set; }
}