using Flatly.Api.Modules.Calendar;
using Flatly.Api.Modules.Chores;
using Flatly.Api.Modules.Flats;
using Flatly.Api.Modules.Maintenance;
using Flatly.Api.Modules.Platform;
using Flatly.Api.Modules.Shopping;

namespace Flatly.Api.Modules;

/// <summary>
/// The single list of modules. Adding a module means adding one line to each method here, next to the others.
/// See <c>api/README.md</c> for the full procedure.
/// </summary>
public static class FlatlyModules
{
    /// <summary>Registers the services of every module.</summary>
    public static IServiceCollection AddFlatlyModules(this IServiceCollection services) =>
        services
            .AddPlatformModule()
            .AddFlatsModule()
            .AddChoresModule()
            .AddShoppingModule()
            .AddCalendarModule()
            .AddMaintenanceModule();

    /// <summary>Maps the endpoints of every module.</summary>
    public static IEndpointRouteBuilder MapFlatlyModules(this IEndpointRouteBuilder app)
    {
        app.MapPlatformModule();
        app.MapFlatsModule();
        app.MapChoresModule();
        app.MapShoppingModule();
        app.MapCalendarModule();
        app.MapMaintenanceModule();
        return app;
    }
}
