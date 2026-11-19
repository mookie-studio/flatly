namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>
/// Schedules the delivery of reminders for later. Callers depend on this interface, never on Hangfire, so the
/// background-job provider can change without touching the features that schedule reminders.
/// Scheduled deliveries are stored in PostgreSQL and survive a restart of the API.
/// </summary>
public interface IReminderScheduler
{
    /// <summary>Schedules the delivery of a reminder at <paramref name="deliverAt"/>. Returns the id to cancel it with.</summary>
    string Schedule(Guid reminderId, DateTimeOffset deliverAt);

    /// <summary>Cancels a scheduled delivery. Returns false when it already ran, was cancelled, or does not exist.</summary>
    bool Cancel(string jobId);
}
