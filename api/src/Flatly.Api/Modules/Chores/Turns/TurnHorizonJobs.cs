namespace Flatly.Api.Modules.Chores.Turns;

/// <summary>
/// The background work that keeps turns ahead of time. Hangfire stores the call and runs it on schedule, and resolves
/// this class from dependency injection for every run.
/// </summary>
public sealed class TurnHorizonJobs(TurnGenerator turns)
{
    /// <summary>Extends the turn horizon of every flat. Safe to run any number of times: it never duplicates a turn.</summary>
    public Task ExtendAsync() => turns.ExtendAllFlatsAsync(CancellationToken.None);
}
