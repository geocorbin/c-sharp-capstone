using Microsoft.EntityFrameworkCore;
using CatalogService.Models;

namespace CatalogService.Data;

public class CatalogServiceContext(DbContextOptions<CatalogServiceContext> options) : DbContext(options)
{
    public DbSet<Book> Books => Set<Book>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Book>(entity =>
        {
            entity.HasKey(b => b.BookId);
            entity.HasIndex(b => b.Isbn).IsUnique();

            entity.Property(b => b.Isbn).IsRequired().HasMaxLength(20);
            entity.Property(b => b.Title).IsRequired().HasMaxLength(255);
            entity.Property(b => b.Author).IsRequired().HasMaxLength(255);
            entity.Property(b => b.Genre).IsRequired().HasMaxLength(100);
            entity.Property(b => b.Publisher).HasMaxLength(255);
            entity.Property(b => b.Language).HasMaxLength(50);
        });
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAuditInfo()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}