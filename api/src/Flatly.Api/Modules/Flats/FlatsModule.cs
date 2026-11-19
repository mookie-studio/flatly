using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Flats.Validation;
using Flatly.Api.Validation;

namespace Flatly.Api.Modules.Flats;

/// <summary>Registration of the Flats module: flats, members and invitations.</summary>
public static class FlatsModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/flats";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddFlatsModule(this IServiceCollection services)
    {
        services.AddSingleton<IRequestValidator<CreateFlatRequest>, CreateFlatRequestValidator>();
        return services;
    }

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapFlatsModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup(RoutePrefix)
            .WithTags("Flats")
            .MapCreateFlat()
            .MapListMyFlats()
            .MapGetFlat();
        return app;
    }
}
