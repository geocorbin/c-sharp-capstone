namespace ReservationService.Dtos;

public class ReservationLimitExceededResponse
{
    public required string Error { get; set; }
    public required string Message { get; set; }
    public int CurrentReservations { get; set; }
}

public class BookUnavailableResponse
{
    public required string Error { get; set; }
    public required string Message { get; set; }
    public int AvailableCopies { get; set; }
}

public class InvalidStatusResponse
{
    public required string Error { get; set; }
    public required string Message { get; set; }
    public required string CurrentStatus { get; set; }
}