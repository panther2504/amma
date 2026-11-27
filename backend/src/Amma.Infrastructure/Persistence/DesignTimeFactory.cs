using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Amma.Infrastructure.Persistence;

/// <summary>Used by `dotnet ef` to create migrations without starting the API.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<AmmaDbContext>
{
    public AmmaDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Amma")
                 ?? "Host=localhost;Database=amma;Username=amma;Password=amma";
        var options = new DbContextOptionsBuilder<AmmaDbContext>().UseNpgsql(cs).Options;
        return new AmmaDbContext(options);
    }
}
