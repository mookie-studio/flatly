using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Finds the Flatly account of the caller of a request. The request is authenticated with the default scheme; the
/// principal's external login (issuer = provider, name identifier = provider subject) identifies the account.
/// Returns null when nobody is signed in, including while no default scheme is configured.
/// </summary>
/// <remarks>
/// This is the one place to change when the cookie session lands: it then reads the session's account instead.
/// </remarks>
public sealed class CurrentMemberResolver(UserManager<FlatlyUser> users, IAuthenticationSchemeProvider schemes)
{
    /// <summary>Returns the account of the signed-in caller, or null when there is none.</summary>
    public async Task<FlatlyUser?> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        // Until the sign-in task configures a default scheme, no request can be signed in.
        if (await schemes.GetDefaultAuthenticateSchemeAsync() is null)
        {
            return null;
        }

        var result = await httpContext.AuthenticateAsync();
        if (!result.Succeeded || result.Principal is null)
        {
            return null;
        }

        var subject = result.Principal.FindFirst(ClaimTypes.NameIdentifier);
        if (subject is null || !ExternalProviders.IsSupported(subject.Issuer))
        {
            return null;
        }

        return await users.FindByLoginAsync(subject.Issuer, subject.Value);
    }
}
