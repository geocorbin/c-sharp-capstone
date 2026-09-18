using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Data;
using CatalogService.Dtos;

namespace CatalogService.Controllers;

[ApiController]
[Route("api/catalog/books")]
public class CatalogController(CatalogServiceContext context) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetBooks(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string sortBy = "title",
        [FromQuery] string sortOrder = "asc",
        [FromQuery] string? query = null,
        [FromQuery] string? genre = null,
        [FromQuery] string? isbn = null,
        [FromQuery] bool availableOnly = false)
    {
        page = Math.Max(page, 0);
        size = Math.Clamp(size, 1, 100);

        var books = context.Books.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowered = query.ToLower();
            books = books.Where(b => b.Title.ToLower().Contains(lowered) || b.Author.ToLower().Contains(lowered));
        }

        if (!string.IsNullOrWhiteSpace(genre))
        {
            books = books.Where(b => b.Genre == genre);
        }

        if (!string.IsNullOrWhiteSpace(isbn))
        {
            books = books.Where(b => b.Isbn == isbn);
        }

        if (availableOnly)
        {
            books = books.Where(b => b.AvailableCopies > 0);
        }

        books = (sortBy.ToLowerInvariant(), sortOrder.ToLowerInvariant()) switch
        {
            ("author", "desc") => books.OrderByDescending(b => b.Author),
            ("author", _) => books.OrderBy(b => b.Author),
            ("publicationyear", "desc") => books.OrderByDescending(b => b.PublicationYear),
            ("publicationyear", _) => books.OrderBy(b => b.PublicationYear),
            ("title", "desc") => books.OrderByDescending(b => b.Title),
            _ => books.OrderBy(b => b.Title)
        };

        var totalElements = await books.CountAsync();
        var totalPages = totalElements == 0 ? 0 : (int)Math.Ceiling(totalElements / (double)size);

        var content = await books
            .Skip(page * size)
            .Take(size)
            .Select(b => new BookSummaryResponse
            {
                BookId = b.BookId,
                Isbn = b.Isbn,
                Title = b.Title,
                Author = b.Author,
                Genre = b.Genre,
                PublicationYear = b.PublicationYear,
                Description = b.Description,
                TotalCopies = b.TotalCopies,
                AvailableCopies = b.AvailableCopies,
                Status = b.AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT"
            })
            .ToListAsync();

        return Ok(new PagedResult<BookSummaryResponse>
        {
            Content = content,
            Page = page,
            Size = size,
            TotalElements = totalElements,
            TotalPages = totalPages,
            Last = page >= totalPages - 1
        });
    }

    [HttpGet("{bookId:guid}")]
    public async Task<IActionResult> GetBookDetails(Guid bookId)
    {
        var book = await context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"Book not found with ID: {bookId}" });
        }

        return Ok(new BookDetailResponse
        {
            BookId = book.BookId,
            Isbn = book.Isbn,
            Title = book.Title,
            Author = book.Author,
            Genre = book.Genre,
            PublicationYear = book.PublicationYear,
            Description = book.Description,
            Publisher = book.Publisher,
            PageCount = book.PageCount,
            Language = book.Language,
            TotalCopies = book.TotalCopies,
            AvailableCopies = book.AvailableCopies,
            Status = book.AvailableCopies > 0 ? "AVAILABLE" : "CHECKED_OUT",
            CreatedAt = book.CreatedAt,
            UpdatedAt = book.UpdatedAt
        });
    }

    [HttpPut("{bookId:guid}/availability")]
    public async Task<IActionResult> UpdateAvailability(Guid bookId, UpdateAvailabilityRequest request)
    {
        var book = await context.Books.FindAsync(bookId);
        if (book is null)
        {
            return NotFound(new ErrorResponse { Error = "NOT_FOUND", Message = $"Book not found with ID: {bookId}" });
        }

        var newAvailable = book.AvailableCopies + request.Delta;
        if (newAvailable < 0 || newAvailable > book.TotalCopies)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "INVALID_AVAILABILITY_UPDATE",
                Message = $"Applying delta {request.Delta} would set availableCopies to {newAvailable}, outside the valid range 0-{book.TotalCopies}."
            });
        }

        book.AvailableCopies = newAvailable;
        await context.SaveChangesAsync();

        return Ok(new UpdateAvailabilityResponse
        {
            BookId = book.BookId,
            AvailableCopies = book.AvailableCopies,
            TotalCopies = book.TotalCopies
        });
    }
}