using Hangfire;
using Microsoft.Extensions.Hosting;

namespace Flatly.Api.Modules.Chores.Turns;

/// <summary>
/// Registers the recurring job that extends the turn horizon, once per start of the API. Registering again replaces the
/// same job (same id), so restarts and several instances never create a second one.
/// </summary>
internal sealed class TurnHorizonSchedule(IRecurringJobManager jobs) : IHostedService
{
    /// <summary>Id of the recurring job in Hangfire.</summary>
    public const string JobId = "chores-extend-turn-horizon";

    /// <summary>Runs every day at 03:00 UTC.</summary>
    public const string Cron = "0 3 * * *";

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        jobs.AddOrUpdate<TurnHorizonJobs>(JobId, job => job.ExtendAsync(), Cron);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
