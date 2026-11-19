namespace Flatly.Api.Modules.Platform;

/// <summary>Registration of the Platform module: cross-cutting concerns such as accounts, sign-in and health.</summary>
public static class PlatformModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/platform";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddPlatformModule(this IServiceCollection services) => services;

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapPlatformModule(this IEndpointRouteBuilder app) =>
        app.MapGroup(RoutePrefix).WithTags("Platform");
}
