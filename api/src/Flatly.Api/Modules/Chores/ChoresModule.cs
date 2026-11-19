namespace Flatly.Api.Modules.Chores;

/// <summary>Registration of the Chores module: chores and their assignments.</summary>
public static class ChoresModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/chores";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddChoresModule(this IServiceCollection services) => services;

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapChoresModule(this IEndpointRouteBuilder app) =>
        app.MapGroup(RoutePrefix).WithTags("Chores");
}
