
namespace ReservationService.Dtos;

public class ReservationResponse
{
    public required Guid ReservationId { get; set; }
    public required Guid BookId { get; set; }
    public required Guid UserId { get; set; }
    public required string BookTitle { get; set; }
    public required string Status { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public required string Message { get; set; }
}