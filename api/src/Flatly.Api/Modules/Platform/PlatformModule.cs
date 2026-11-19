using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Email;
using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Modules.Platform.Scheduling;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
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
        ConfigureSignIn(services);
        services.AddScoped<CurrentAccountResolver>();
        services.AddSingleton<IRequestValidator<UpdateProfileRequest>, UpdateProfileRequestValidator>();
        // Notification emails go through IEmailSender: SMTP locally, Resend in deployment (see api/README.md).
        services.AddEmailSender();
        // Background jobs (Hangfire on PostgreSQL) run scheduled reminders. Features reach them through IReminderScheduler.
        services.AddReminderScheduling();
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
            .MapAuthEndpoints()
            .MapValidationSample()
            .MapClockSample()
            .MapProfile()
            .MapAccount();
        return app;
    }

    /// <summary>
    /// Google and Microsoft sign-in, the cookie session and default-deny authorization. Every endpoint requires a signed-in
    /// member unless it says <c>AllowAnonymous</c>; that is the only way to make an endpoint public.
    /// </summary>
    private static void ConfigureSignIn(IServiceCollection services)
    {
        services.AddAuthentication(SessionAuthentication.Scheme)
            .AddCookie(SessionAuthentication.Scheme, SessionAuthentication.ConfigureCookie)
            .AddGoogle()
            .AddMicrosoftAccount();
        services.AddOptions<GoogleOptions>(GoogleDefaults.AuthenticationScheme)
            .Configure<IConfiguration>(GoogleSignIn.Configure);
        services.AddOptions<MicrosoftAccountOptions>(MicrosoftAccountDefaults.AuthenticationScheme)
            .Configure<IConfiguration>(MicrosoftSignIn.Configure);

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        // The antiforgery cookie goes on cross-site requests too (SameSite=None). It is Secure on HTTPS, which is the only
        // scheme App Service and Cloudflare serve; over plain HTTP in local development it is not Secure, since
        // ASP.NET Core refuses to issue a Secure cookie on an HTTP request.
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-XSRF-TOKEN";
            options.Cookie.Name = "Flatly.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.None;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
    }
}
