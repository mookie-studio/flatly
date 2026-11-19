using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// The cookie session the API issues once an external sign-in completes (ADR-003). The web app never stores provider
/// tokens; it only carries this cookie. The cookie is the default scheme, so every endpoint is authenticated by it
/// unless it is marked <c>AllowAnonymous</c>.
/// </summary>
public static class SessionAuthentication
{
    /// <summary>Name of the authentication scheme that reads and writes the session cookie.</summary>
    public const string Scheme = "Flatly.Session";

    /// <summary>Name of the session cookie. It is HttpOnly, so scripts on the page cannot read it.</summary>
    public const string CookieName = "Flatly.Session";

    /// <summary>How long a session lasts without activity. Each request in the second half of the window renews it.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    /// <summary>
    /// Configures the cookie for a web app on another origin than the API (Cloudflare Pages and Azure App Service):
    /// it is sent on cross-site requests (SameSite=None), so it is always Secure and HttpOnly.
    /// API requests that are not signed in get 401 and 403, not a redirect to a login page.
    /// </summary>
    public static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = CookieName;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.None;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = Lifetime;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    /// <summary>The principal a signed-in member carries. The name identifier is the Flatly account id, never a provider subject.</summary>
    public static ClaimsPrincipal PrincipalFor(FlatlyUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
                new Claim(ClaimTypes.Name, user.DisplayName),
            ],
            Scheme);
        return new ClaimsPrincipal(identity);
    }
}
