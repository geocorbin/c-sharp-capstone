namespace ReservationService.Dtos;

public class WaitlistJoinResponse
{
    public required Guid WaitlistId { get; set; }
    public required Guid BookId { get; set; }
    public required string BookTitle { get; set; }
    public required string Status { get; set; }
    public DateTime JoinedAt { get; set; }
    public int Position { get; set; }
}
