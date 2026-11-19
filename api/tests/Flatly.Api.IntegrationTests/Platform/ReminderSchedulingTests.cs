using System.Net;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Platform.Scheduling;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Platform;

/// <summary>
/// Schedules reminders through <see cref="IReminderScheduler"/> on a real PostgreSQL and checks that Hangfire keeps
/// them when the API restarts. Each restart is a new host on the same database, as a redeploy would be.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ReminderSchedulingTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task ScheduledReminderSurvivesARestartOfTheApi()
    {
        var connectionString = await ConnectionStringAsync();
        string jobId;

        await using (var before = new FlatlyApiFactory(connectionString, Now))
        {
            var scheduler = before.Services.GetRequiredService<IReminderScheduler>();
            jobId = scheduler.Schedule(Guid.NewGuid(), Now.AddDays(3));
        }

        await using var after = new FlatlyApiFactory(connectionString, Now);
        using var client = after.CreateClient();
        var scheduled = JobStorage.Current.GetMonitoringApi().ScheduledJobs(0, 100);

        Assert.Contains(scheduled, job => job.Key == jobId);
    }

    [SkippableFact]
    public async Task CancelRemovesTheScheduledReminder()
    {
        var connectionString = await ConnectionStringAsync();
        await using var factory = new FlatlyApiFactory(connectionString, Now);
        var scheduler = factory.Services.GetRequiredService<IReminderScheduler>();
        var jobId = scheduler.Schedule(Guid.NewGuid(), Now.AddDays(3));

        var cancelled = scheduler.Cancel(jobId);

        Assert.True(cancelled);
        Assert.DoesNotContain(JobStorage.Current.GetMonitoringApi().ScheduledJobs(0, 100), job => job.Key == jobId);
    }

    [SkippableFact]
    public async Task DashboardIsServedInDevelopment()
    {
        var connectionString = await ConnectionStringAsync();
        await using var factory = new FlatlyApiFactory(connectionString, Now);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> ConnectionStringAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return await postgres.ConnectionStringAsync();
    }
}
