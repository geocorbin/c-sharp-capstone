namespace ReservationService.Dtos;

public class ActiveReservationItem
{
    public required Guid ReservationId { get; set; }
    public required Guid BookId { get; set; }
    public required string BookTitle { get; set; }
    public required string BookAuthor { get; set; }
    public required string Status { get; set; }
    public DateTime? ReservedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? CheckedOutAt { get; set; }
    public DateTime? DueDate { get; set; }
    public int? DaysUntilExpiry { get; set; }
    public int? DaysUntilDue { get; set; }
}

public class ActiveReservationsResponse
{
    public required List<ActiveReservationItem> Reservations { get; set; }
    public int TotalActive { get; set; }
}
