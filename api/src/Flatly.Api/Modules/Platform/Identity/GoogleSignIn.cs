using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Google as an external provider. The provider handles the redirect to Google and the callback at <c>/signin-google</c>
/// (see README for the registered URI). When Google reports the account, the API finds or creates the Flatly account and
/// signs the browser in with the session cookie. Provider tokens are not kept.
/// </summary>
public static class GoogleSignIn
{
    /// <summary>Claim that carries the Google picture URL between the ticket and the account.</summary>
    public const string PictureClaim = "urn:flatly:picture";

    /// <summary>Configuration section with the Google client id and secret.</summary>
    public const string ConfigurationSection = "Authentication:Google";

    /// <summary>Configuration key of the web app origin, used to send the browser back after sign-in.</summary>
    public const string WebOriginKey = "Web:Origin";

    /// <summary>Where the browser lands when Google sign-in fails, relative to the web origin.</summary>
    public const string FailurePath = "/sign-in?error=provider";

    /// <summary>Applies the configuration and the sign-in events to the Google options.</summary>
    public static void Configure(GoogleOptions options, IConfiguration configuration)
    {
        options.ClientId = configuration[$"{ConfigurationSection}:ClientId"] ?? string.Empty;
        options.ClientSecret = configuration[$"{ConfigurationSection}:ClientSecret"] ?? string.Empty;
        // The Flatly account, not Google, is the principal the rest of the API sees.
        options.SignInScheme = SessionAuthentication.Scheme;

        options.Events.OnCreatingTicket = context =>
        {
            if (context.User.TryGetProperty("picture", out var picture) && picture.ValueKind == JsonValueKind.String)
            {
                context.Identity?.AddClaim(new Claim(PictureClaim, picture.GetString()!));
            }

            return Task.CompletedTask;
        };

        options.Events.OnTicketReceived = async context =>
        {
            var principal = context.Principal;
            var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (principal is null || string.IsNullOrWhiteSpace(subject))
            {
                context.Fail("Google did not return an account id.");
                return;
            }

            var accounts = context.HttpContext.RequestServices.GetRequiredService<ExternalAccountService>();
            var user = await accounts.FindOrCreateAsync(new ExternalIdentity(
                ExternalProviders.Google,
                subject,
                principal.FindFirstValue(ClaimTypes.Email),
                principal.FindFirstValue(ClaimTypes.Name),
                principal.FindFirstValue(PictureClaim)),
                context.HttpContext.RequestAborted);

            context.Principal = SessionAuthentication.PrincipalFor(user);
            context.ReturnUri = ReturnUrl.Resolve(context.Properties?.RedirectUri, configuration[WebOriginKey]);
        };

        options.Events.OnRemoteFailure = context =>
        {
            // The member cancelled or Google refused. The web app shows a message instead of a server error page.
            var webOrigin = configuration[WebOriginKey];
            var target = string.IsNullOrWhiteSpace(webOrigin) ? FailurePath : webOrigin.TrimEnd('/') + FailurePath;
            context.Response.Redirect(target);
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }
}
