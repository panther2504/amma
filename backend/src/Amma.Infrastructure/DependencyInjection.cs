using Amma.Infrastructure.Persistence;
using Amma.Infrastructure.Seed;
using Amma.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Amma.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the database and storage. Uses PostgreSQL when ConnectionStrings:Amma is set,
    /// otherwise an in-memory database so the app can run before the database is provisioned.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var cs = config.GetConnectionString("Amma");
        services.AddDbContext<AmmaDbContext>(o =>
        {
            if (string.IsNullOrWhiteSpace(cs))
                o.UseInMemoryDatabase(config["Database:InMemoryName"] ?? "amma");
            else
                o.UseNpgsql(cs, npg => npg.EnableRetryOnFailure(3));
        });
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}
