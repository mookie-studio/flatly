using Flatly.Api.Data;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>Registers background jobs (Hangfire on PostgreSQL) and the reminder scheduler built on them.</summary>
public static class SchedulingServiceCollectionExtensions
{
    /// <summary>
    /// Hangfire keeps its tables in the <c>hangfire</c> schema of the same database, created on first start, so no EF
    /// migration is involved. The server that runs the jobs starts with the API, so one App Service instance runs them.
    /// </summary>
    public static IServiceCollection AddReminderScheduling(this IServiceCollection services)
    {
        services.AddHangfire((serviceProvider, configuration) =>
        {
            var connectionString = serviceProvider.GetRequiredService<IConfiguration>()
                .GetConnectionString(DatabaseRegistration.ConnectionName)
                ?? throw new InvalidOperationException($"The connection string '{DatabaseRegistration.ConnectionName}' is not configured.");

            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
        });
        services.AddHangfireServer();
        services.TryAddTransient<ReminderJobs>();
        services.TryAddSingleton<IReminderScheduler, HangfireReminderScheduler>();
        return services;
    }
}
