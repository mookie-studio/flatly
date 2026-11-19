namespace Flatly.Api.Modules.Maintenance;

/// <summary>Registration of the Maintenance module: landlord communication, incidents and the home pack.</summary>
public static class MaintenanceModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/maintenance";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddMaintenanceModule(this IServiceCollection services) => services;

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapMaintenanceModule(this IEndpointRouteBuilder app) =>
        app.MapGroup(RoutePrefix).WithTags("Maintenance");
}
