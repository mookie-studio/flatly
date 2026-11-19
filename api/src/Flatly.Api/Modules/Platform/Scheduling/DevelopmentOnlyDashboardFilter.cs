using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Flatly.Api.Modules.Platform.Scheduling;

/// <summary>
/// Lets the Hangfire dashboard answer only in the Development environment. Production has no platform admins yet,
/// so the dashboard is not served there at all: the only way to see the jobs is locally.
/// </summary>
internal sealed class DevelopmentOnlyDashboardFilter : IDashboardAuthorizationFilter
{
    /// <inheritdoc />
    public bool Authorize(DashboardContext context) =>
        context.GetHttpContext().RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment();
}
