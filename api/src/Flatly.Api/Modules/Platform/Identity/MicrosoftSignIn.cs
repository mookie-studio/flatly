using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Microsoft as an external provider, for work, school and personal accounts (Outlook, Hotmail, Live). The authorization
/// and token endpoints use the <c>common</c> tenant, so every Microsoft audience can sign in. The provider handles the
/// redirect to Microsoft and the callback at <c>/signin-microsoft</c> (see README for the registered URI). When Microsoft
/// reports the account, the API finds or creates the Flatly account and signs the browser in with the session cookie.
/// Provider tokens are not kept.
/// </summary>
public static class MicrosoftSignIn
{
    /// <summary>Configuration section with the Microsoft client id and secret.</summary>
    public const string ConfigurationSection = "Authentication:Microsoft";

    /// <summary>Authorization endpoint for every Microsoft audience (work, school and personal accounts).</summary>
    public const string AuthorizationEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize";

    /// <summary>Token endpoint for every Microsoft audience (work, school and personal accounts).</summary>
    public const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    /// <summary>Microsoft Graph endpoint that returns the signed-in account's id, name and email.</summary>
    public const string UserInformationEndpoint = "https://graph.microsoft.com/v1.0/me";

    /// <summary>Applies the configuration and the sign-in events to the Microsoft account options.</summary>
    public static void Configure(MicrosoftAccountOptions options, IConfiguration configuration)
    {
        options.ClientId = configuration[$"{ConfigurationSection}:ClientId"] ?? string.Empty;
        options.ClientSecret = configuration[$"{ConfigurationSection}:ClientSecret"] ?? string.Empty;
        // The Flatly account, not Microsoft, is the principal the rest of the API sees.
        options.SignInScheme = SessionAuthentication.Scheme;
        options.AuthorizationEndpoint = AuthorizationEndpoint;
        options.TokenEndpoint = TokenEndpoint;
        options.UserInformationEndpoint = UserInformationEndpoint;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("User.Read");

        options.Events.OnCreatingTicket = context =>
        {
            // Personal accounts often have no `mail` value, so the sign-in name (userPrincipalName) is the fallback for the prefill.
            if (context.Identity is { } identity && !identity.HasClaim(claim => claim.Type == ClaimTypes.Email))
            {
                var email = FirstString(context.User, "mail") ?? FirstString(context.User, "userPrincipalName");
                if (email is not null)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Email, email));
                }
            }

            return Task.CompletedTask;
        };

        options.Events.OnTicketReceived = async context =>
        {
            var principal = context.Principal;
            var subject = principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (principal is null || string.IsNullOrWhiteSpace(subject))
            {
                context.Fail("Microsoft did not return an account id.");
                return;
            }

            var accounts = context.HttpContext.RequestServices.GetRequiredService<ExternalAccountService>();
            var user = await accounts.FindOrCreateAsync(new ExternalIdentity(
                ExternalProviders.Microsoft,
                subject,
                principal.FindFirstValue(ClaimTypes.Email),
                principal.FindFirstValue(ClaimTypes.Name)),
                context.HttpContext.RequestAborted);

            context.Principal = SessionAuthentication.PrincipalFor(user);
            context.ReturnUri = ReturnUrl.Resolve(context.Properties?.RedirectUri, configuration[GoogleSignIn.WebOriginKey]);
        };

        options.Events.OnRemoteFailure = context =>
        {
            // The member cancelled or Microsoft refused. The web app shows a message instead of a server error page.
            var webOrigin = configuration[GoogleSignIn.WebOriginKey];
            var target = string.IsNullOrWhiteSpace(webOrigin) ? GoogleSignIn.FailurePath : webOrigin.TrimEnd('/') + GoogleSignIn.FailurePath;
            context.Response.Redirect(target);
            context.HandleResponse();
            return Task.CompletedTask;
        };
    }

    private static string? FirstString(JsonElement user, string property) =>
        user.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
}
