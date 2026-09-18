namespace CatalogService.Models;

public class Book : IAuditable
{
    public Guid BookId { get; set; } = Guid.NewGuid();

    public required string Isbn { get; set; }

    public required string Title { get; set; }

    public required string Author { get; set; }

    public required string Genre { get; set; }

    public int? PublicationYear { get; set; }

    public string? Description { get; set; }

    public string? Publisher { get; set; }

    public int? PageCount { get; set; }

    public string? Language { get; set; }

    public int TotalCopies { get; set; }

    public int AvailableCopies { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}