namespace ReservationService.Dtos;

public class HistoryItemResponse
{
    public required Guid ReservationId { get; set; }
    public required string BookTitle { get; set; }
    public required string BookAuthor { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public required string Status { get; set; }
    public bool WasLate { get; set; }
}