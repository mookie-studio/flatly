using Hangfire;
using Microsoft.AspNetCore.Builder;

namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>Serves the Hangfire dashboard, the page that shows the scheduled and failed jobs.</summary>
public static class SchedulingApplicationBuilderExtensions
{
    /// <summary>Route of the dashboard.</summary>
    public const string DashboardPath = "/hangfire";

    /// <summary>
    /// Serves the dashboard at <see cref="DashboardPath"/>. Call it only in Development: the host calls it from
    /// <c>Program</c> under that condition, and <see cref="DevelopmentOnlyDashboardFilter"/> refuses the page otherwise.
    /// </summary>
    public static IApplicationBuilder UseJobDashboard(this IApplicationBuilder app) =>
        app.UseHangfireDashboard(DashboardPath, new DashboardOptions
        {
            Authorization = [new DevelopmentOnlyDashboardFilter()],
        });
}
