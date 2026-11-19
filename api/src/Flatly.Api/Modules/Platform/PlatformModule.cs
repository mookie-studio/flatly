using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Flatly.Api.Modules.Platform;

/// <summary>Registration of the Platform module: cross-cutting concerns such as accounts, sign-in and health.</summary>
public static class PlatformModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/platform";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddPlatformModule(this IServiceCollection services)
    {
        services.AddFlatlyDatabase();
        // ASP.NET Core Identity stores accounts and external logins. Passwordless: Google and Microsoft are the only
        // ways in (ADR-003), so no password or email confirmation is configured. The cookie session comes later.
        services.AddIdentityCore<FlatlyUser>(options =>
            {
                options.User.RequireUniqueEmail = false;
                options.SignIn.RequireConfirmedAccount = false;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<FlatlyDbContext>();
        services.AddScoped<ExternalAccountService>();
        services.AddScoped<CurrentAccountResolver>();
        // Every part of the API reads the current time through TimeProvider, never DateTime.UtcNow, so tests can fake it.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IRequestValidator<ValidationSampleRequest>, ValidationSampleRequestValidator>();
        return services;
    }

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapPlatformModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup(RoutePrefix)
            .WithTags("Platform")
            .MapValidationSample()
            .MapClockSample();
        return app;
    }
}
