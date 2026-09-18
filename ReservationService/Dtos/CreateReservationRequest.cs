namespace ReservationService.Dtos;

public class CreateReservationRequest
{
    public required Guid BookId { get; set; }
}
