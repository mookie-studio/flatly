using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Finds the Flatly account of the signed-in request. A provider sign-in puts the provider's subject in the
/// name identifier claim, with the provider as its issuer (see <see cref="ExternalProviders"/>). The account is
/// looked up by that pair, the same way <see cref="ExternalAccountService"/> matches accounts.
/// Returns null when nobody is signed in or the account does not exist yet.
/// </summary>
public sealed class CurrentAccountResolver(UserManager<FlatlyUser> users)
{
    /// <summary>Returns the account of <paramref name="principal"/>, or null when there is none.</summary>
    public async Task<FlatlyUser?> FindAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (principal.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var claim = principal.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !ExternalProviders.IsSupported(claim.Issuer))
        {
            return null;
        }

        return await users.FindByLoginAsync(claim.Issuer, claim.Value);
    }
}
