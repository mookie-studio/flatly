namespace Flatly.Api.Modules.Shopping;

/// <summary>Registration of the Shopping module: shopping lists and purchases (MVP2).</summary>
public static class ShoppingModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/shopping";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddShoppingModule(this IServiceCollection services) => services;

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapShoppingModule(this IEndpointRouteBuilder app) =>
        app.MapGroup(RoutePrefix).WithTags("Shopping");
}
