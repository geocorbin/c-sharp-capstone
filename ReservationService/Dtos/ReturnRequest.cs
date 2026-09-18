namespace ReservationService.Dtos;

public class ReturnRequest
{
    public required string Condition { get; set; }
    public string? Notes { get; set; }
}