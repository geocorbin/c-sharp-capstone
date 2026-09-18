using Microsoft.EntityFrameworkCore;
using CatalogService.Models;

namespace CatalogService.Data;

public static class DataSeeder
{
    public static async Task SeedAsync(CatalogServiceContext context)
    {
        if (await context.Books.AnyAsync())
        {
            return;
        }

        context.Books.AddRange(
            new Book { Isbn = "978-0-13-468599-1", Title = "Clean Code", Author = "Robert C. Martin", Genre = "Technology", PublicationYear = 2008, TotalCopies = 5, AvailableCopies = 2 },
            new Book { Isbn = "978-0-13-475759-9", Title = "Refactoring", Author = "Martin Fowler", Genre = "Technology", PublicationYear = 2018, TotalCopies = 3, AvailableCopies = 0 },
            new Book { Isbn = "978-0-452-28423-4", Title = "1984", Author = "George Orwell", Genre = "Fiction", PublicationYear = 1949, TotalCopies = 4, AvailableCopies = 4 },
            new Book { Isbn = "978-0-06-085052-4", Title = "Brave New World", Author = "Aldous Huxley", Genre = "Fiction", PublicationYear = 1932, TotalCopies = 2, AvailableCopies = 1 },
            new Book { Isbn = "978-0-544-00341-5", Title = "The Hobbit", Author = "J.R.R. Tolkien", Genre = "Fantasy", PublicationYear = 1937, TotalCopies = 6, AvailableCopies = 3 }
        );

        await context.SaveChangesAsync();
    }
}