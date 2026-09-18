namespace ReservationService.Models;

public class Reservation : IAuditable
{
    public Guid ReservationId { get; set; } = Guid.NewGuid();

    public required Guid BookId { get; set; }

    public required Guid UserId { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Reserved;

    public DateTime ReservedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? CheckedOutAt { get; set; }

    public DateTime? DueDate { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public int RenewalCount { get; set; }

    public int? LateDays { get; set; }

    public decimal? LateFee { get; set; }

    public BookCondition? Condition { get; set; }

    public string? Notes { get; set; }
    
    public required string BookTitle { get; set; }

    public required string BookAuthor { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}