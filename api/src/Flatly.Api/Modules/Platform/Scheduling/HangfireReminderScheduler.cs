using System.Linq.Expressions;
using Hangfire;

namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>The <see cref="IReminderScheduler"/> that stores its jobs in Hangfire.</summary>
internal sealed class HangfireReminderScheduler(IBackgroundJobClient jobs) : IReminderScheduler
{
    /// <inheritdoc />
    public string Schedule(Guid reminderId, DateTimeOffset deliverAt)
    {
        Expression<Func<ReminderJobs, Task>> call = job => job.DeliverAsync(reminderId);
        return jobs.Schedule(call, deliverAt);
    }

    /// <inheritdoc />
    public bool Cancel(string jobId) => jobs.Delete(jobId);
}
