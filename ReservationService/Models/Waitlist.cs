namespace ReservationService.Models;

public class Waitlist : IAuditable
{
    public Guid WaitlistId { get; set; } = Guid.NewGuid();

    public required Guid BookId { get; set; }

    public required Guid UserId { get; set; }

    public WaitlistStatus Status { get; set; } = WaitlistStatus.Waiting;

    public DateTime JoinedAt { get; set; }

    // Set when a held copy is offered to this entry.
    public DateTime? NotifiedAt { get; set; }

    // NotifiedAt + 48 hours, set alongside NotifiedAt.
    public DateTime? ClaimDeadline { get; set; }

    // Set to the auto-created Reservation's Id once this entry is claimed.
    public Guid? ResultingReservationId { get; set; }

    // Cached from Catalog Service - same reasoning as Reservation.BookTitle/BookAuthor.
    public required string BookTitle { get; set; }

    public required string BookAuthor { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}