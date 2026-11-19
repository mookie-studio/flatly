using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;

namespace Flatly.Api.Modules.Platform.Endpoints;

/// <summary>The member the session belongs to, as the web app shows it.</summary>
/// <param name="UserId">The Flatly account id.</param>
/// <param name="DisplayName">Name shown to the other members of a flat.</param>
/// <param name="AvatarUrl">Picture URL, or null.</param>
/// <param name="PreferredLanguage">Interface language, <c>es</c> or <c>en</c>.</param>
/// <param name="TimeZoneId">IANA time zone identifier.</param>
public sealed record SessionResponse(Guid UserId, string DisplayName, string? AvatarUrl, string PreferredLanguage, string TimeZoneId);

/// <summary>Token the browser sends in the <c>X-XSRF-TOKEN</c> header of a state-changing request.</summary>
/// <param name="RequestToken">The antiforgery request token.</param>
public sealed record AntiforgeryTokenResponse(string RequestToken);

/// <summary>
/// Sign-in and session endpoints. Sign-in and sign-out are public (the member is not signed in yet, or already out);
/// the session endpoint needs the cookie. Sign-out is a state change, so it validates an antiforgery token.
/// </summary>
public static class AuthEndpoints
{
    /// <summary>Maps the sign-in, session and sign-out endpoints under the given group.</summary>
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/auth/google/sign-in", (string? returnUrl, IConfiguration configuration) =>
                Results.Challenge(
                    new AuthenticationProperties { RedirectUri = ReturnUrl.Resolve(returnUrl, configuration[GoogleSignIn.WebOriginKey]) },
                    [GoogleDefaults.AuthenticationScheme]))
            .AllowAnonymous()
            .WithName("SignInWithGoogle")
            .WithSummary("Starts the Google sign-in. Redirects to Google, then back to the web app with the session cookie set.");

        group.MapGet("/auth/session", async (ClaimsPrincipal principal, UserManager<FlatlyUser> users) =>
            {
                var user = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
                    ? await users.FindByIdAsync(id.ToString("D"))
                    : null;
                return user is null
                    ? Results.Unauthorized()
                    : TypedResults.Ok(new SessionResponse(user.Id, user.DisplayName, user.AvatarUrl, user.PreferredLanguage, user.TimeZoneId));
            })
            .WithName("GetSession")
            .WithSummary("The signed-in member. Returns 401 when there is no session.");

        group.MapGet("/auth/antiforgery-token", (IAntiforgery antiforgery, HttpContext httpContext) =>
                TypedResults.Ok(new AntiforgeryTokenResponse(antiforgery.GetAndStoreTokens(httpContext).RequestToken!)))
            .AllowAnonymous()
            .WithName("GetAntiforgeryToken")
            .WithSummary("Issues the antiforgery token that state-changing requests send in the X-XSRF-TOKEN header.");

        group.MapPost("/auth/sign-out", async Task<IResult> (HttpContext httpContext, IAntiforgery antiforgery) =>
            {
                // Checked here, not by the middleware: minimal API endpoints without a form are not validated automatically.
                if (!await antiforgery.IsRequestValidAsync(httpContext))
                {
                    return Results.BadRequest();
                }

                await httpContext.SignOutAsync(SessionAuthentication.Scheme);
                return Results.NoContent();
            })
            .AllowAnonymous()
            .WithName("SignOut")
            .WithSummary("Ends the session. Requires the antiforgery token in the X-XSRF-TOKEN header.");

        return group;
    }
}
