namespace CatalogService.Dtos;

public class UpdateAvailabilityResponse
{
    public required Guid BookId { get; set; }
    public required int AvailableCopies { get; set; }
    public required int TotalCopies { get; set; }
}