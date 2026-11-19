using Microsoft.Extensions.Logging;

namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>
/// The work that Hangfire runs when a reminder is due. Hangfire stores the call (this method and its arguments)
/// in PostgreSQL, so it runs after a restart too. Resolved from dependency injection for every run.
/// </summary>
public sealed partial class ReminderJobs(ILogger<ReminderJobs> logger)
{
    /// <summary>Delivers one due reminder.</summary>
    public Task DeliverAsync(Guid reminderId)
    {
        // Placeholder until reminders have content and recipients (notifications, #62). Replace the log with the delivery.
        LogReminderDue(logger, reminderId);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder {ReminderId} is due.")]
    private static partial void LogReminderDue(ILogger logger, Guid reminderId);
}
