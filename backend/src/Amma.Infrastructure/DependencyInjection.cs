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
        // Hosts like Render/Neon provide a postgres:// URI (often as DATABASE_URL); Npgsql needs key=value form.
        var cs = ConnectionStrings.ToNpgsql(config.GetConnectionString("Amma") is { Length: > 0 } c ? c : config["DATABASE_URL"]);
        services.AddDbContext<AmmaDbContext>(o =>
        {
            if (string.IsNullOrWhiteSpace(cs))
                o.UseInMemoryDatabase(config["Database:InMemoryName"] ?? "amma");
            else
                o.UseNpgsql(cs, npg => npg.EnableRetryOnFailure(3));
        });
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<FileCrypto>();
        // "Database" keeps documents in PostgreSQL (needed on free hosts, which have no persistent disk).
        if (string.Equals(config["Storage:Provider"], "Database", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IFileStorage, DatabaseFileStorage>();
        else
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<DatabaseInitializer>();
        return services;
    }
}

public static class ConnectionStrings
{
    /// <summary>Converts postgres://user:pass@host:port/db?sslmode=require into an Npgsql connection string.</summary>
    public static string? ToNpgsql(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return value;
        // Kerberos/GSS is never used with hosted Postgres such as Neon; disabling it avoids a native-library probe.
        var uri = new Uri(value);
        var userInfo = uri.UserInfo.Split(':', 2);
        var b = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port <= 0 ? 5432 : uri.Port,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        };
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var ssl = query["sslmode"];
        if (!string.IsNullOrEmpty(ssl) && Enum.TryParse<Npgsql.SslMode>(ssl.Replace("-", ""), true, out var mode)) b.SslMode = mode;
        else if (!uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) b.SslMode = Npgsql.SslMode.Require;
        b.GssEncryptionMode = Npgsql.GssEncryptionMode.Disable;
        return b.ConnectionString;
    }
}
