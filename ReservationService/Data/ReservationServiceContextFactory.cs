using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReservationService.Data;

public class ReservationServiceContextFactory : IDesignTimeDbContextFactory<ReservationServiceContext>
{
    public ReservationServiceContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReservationServiceContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design_time_only;Username=postgres;Password=postgres");
        return new ReservationServiceContext(optionsBuilder.Options);
    }
}
