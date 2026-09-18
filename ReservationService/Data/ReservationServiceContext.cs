using Microsoft.EntityFrameworkCore;
using ReservationService.Models;

namespace ReservationService.Data;

public class ReservationServiceContext(DbContextOptions<ReservationServiceContext> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Waitlist> Waitlists => Set<Waitlist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.ReservationId);
            // Both queried constantly: "my active reservations", "reservations for this book".
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.BookId);

            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.Condition).HasConversion<string>().HasMaxLength(20);
            entity.Property(r => r.LateFee).HasPrecision(10, 2);
            entity.Property(r => r.BookTitle).IsRequired().HasMaxLength(255);
            entity.Property(r => r.BookAuthor).IsRequired().HasMaxLength(255);
        });

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.HasKey(w => w.WaitlistId);
            // Both queried constantly: "this book's queue", "my waitlist entries".
            entity.HasIndex(w => w.BookId);
            entity.HasIndex(w => w.UserId);

            entity.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(w => w.BookTitle).IsRequired().HasMaxLength(255);
            entity.Property(w => w.BookAuthor).IsRequired().HasMaxLength(255);
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