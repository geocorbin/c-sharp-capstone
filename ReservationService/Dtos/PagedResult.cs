namespace ReservationService.Dtos;

public class PagedResult<T>
{
    public required List<T> Content { get; set; }
    public int Page { get; set; }
    public int Size { get; set; }
    public long TotalElements { get; set; }
    public int TotalPages { get; set; }
    public bool Last { get; set; }
}