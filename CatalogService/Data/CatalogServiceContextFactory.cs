using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CatalogService.Data;

public class CatalogServiceContextFactory : IDesignTimeDbContextFactory<CatalogServiceContext>
{
    public CatalogServiceContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogServiceContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design_time_only;Username=postgres;Password=postgres");
        return new CatalogServiceContext(optionsBuilder.Options);
    }
}