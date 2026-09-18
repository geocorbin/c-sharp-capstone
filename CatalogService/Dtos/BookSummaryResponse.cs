namespace CatalogService.Dtos;

public class BookSummaryResponse
{
    public required Guid BookId { get; set; }
    public required string Isbn { get; set; }
    public required string Title { get; set; }
    public required string Author { get; set; }
    public required string Genre { get; set; }
    public int? PublicationYear { get; set; }
    public string? Description { get; set; }
    public int TotalCopies { get; set; }
    public int AvailableCopies { get; set; }
    public required string Status { get; set; }
}