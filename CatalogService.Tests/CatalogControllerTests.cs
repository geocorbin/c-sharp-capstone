using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CatalogService.Controllers;
using CatalogService.Data;
using CatalogService.Dtos;
using CatalogService.Models;

namespace CatalogService.Tests;

public class CatalogControllerTests
{
    private static async Task<CatalogServiceContext> SeedThreeBooksAsync()
    {
        var options = new DbContextOptionsBuilder<CatalogServiceContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CatalogServiceContext(options);

        context.Books.AddRange(
            new Book { Isbn = "1", Title = "Clean Code", Author = "Robert C. Martin", Genre = "Technology", TotalCopies = 5, AvailableCopies = 2 },
            new Book { Isbn = "2", Title = "Refactoring", Author = "Martin Fowler", Genre = "Technology", TotalCopies = 3, AvailableCopies = 0 },
            new Book { Isbn = "3", Title = "1984", Author = "George Orwell", Genre = "Fiction", TotalCopies = 4, AvailableCopies = 4 }
        );
        await context.SaveChangesAsync();
        return context;
    }

    [Fact]
    public async Task GetBooks_QueryMatchesTitle_ReturnsOnlyMatchingBook()
    {
        var controller = new CatalogController(await SeedThreeBooksAsync());

        var result = await controller.GetBooks(query: "clean");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Single(page.Content);
        Assert.Equal("Clean Code", page.Content[0].Title);
    }

    [Fact]
    public async Task GetBooks_AvailableOnlyTrue_ExcludesBooksWithZeroCopies()
    {
        var controller = new CatalogController(await SeedThreeBooksAsync());

        var result = await controller.GetBooks(availableOnly: true);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Equal(2, page.Content.Count);
        Assert.DoesNotContain(page.Content, b => b.Title == "Refactoring");
    }

    [Fact]
    public async Task GetBooks_SortByAuthorDescending_OrdersCorrectly()
    {
        var controller = new CatalogController(await SeedThreeBooksAsync());

        var result = await controller.GetBooks(sortBy: "author", sortOrder: "desc");

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<PagedResult<BookSummaryResponse>>(ok.Value);
        Assert.Equal(
            new[] { "Robert C. Martin", "Martin Fowler", "George Orwell" },
            page.Content.Select(b => b.Author));
    }

    [Theory]
    [InlineData(-3, false)]
    [InlineData(-2, true)]
    [InlineData(1, true)]
    [InlineData(4, false)]
    public async Task UpdateAvailability_DeltaAtOrBeyondBounds_RespondsAccordingly(int delta, bool shouldSucceed)
    {
        var context = await SeedThreeBooksAsync();
        var book = await context.Books.SingleAsync(b => b.Title == "Clean Code");
        var controller = new CatalogController(context);

        var result = await controller.UpdateAvailability(book.BookId, new UpdateAvailabilityRequest { Delta = delta });

        Assert.IsType(shouldSucceed ? typeof(OkObjectResult) : typeof(BadRequestObjectResult), result);
    }
}