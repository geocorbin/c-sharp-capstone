using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UserService.Data;

public class UserServiceContextFactory : IDesignTimeDbContextFactory<UserServiceContext>
{
    public UserServiceContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<UserServiceContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=design_time_only;Username=postgres;Password=postgres");
        return new UserServiceContext(optionsBuilder.Options);
    }
}