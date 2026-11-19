using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Data;

/// <summary>Registers the PostgreSQL database and applies its migrations.</summary>
public static class DatabaseRegistration
{
    /// <summary>
    /// Name of the connection string. Aspire injects it from the <c>flatly</c> database in the AppHost;
    /// App Service reads it from <c>ConnectionStrings__flatly</c>.
    /// </summary>
    public const string ConnectionName = "flatly";

    /// <summary>Registers <see cref="FlatlyDbContext"/> on PostgreSQL with snake_case column names.</summary>
    public static IServiceCollection AddFlatlyDatabase(this IServiceCollection services) =>
        services.AddDbContext<FlatlyDbContext>((serviceProvider, options) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"The connection string '{ConnectionName}' is not configured.");

            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
        });

    /// <summary>Applies every pending migration. Used by the <c>migrate</c> command and, in development, at startup.</summary>
    public static async Task MigrateDatabaseAsync(this IHost host)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        await database.Database.MigrateAsync();
    }
}
