using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace Flatly.Api.Modules.Platform.Identity;

/// <summary>
/// Finds the Flatly account of the signed-in request. The session cookie carries the account id as its name identifier
/// (see <see cref="SessionAuthentication.PrincipalFor"/>), so the account is looked up by that id.
/// Returns null when nobody is signed in or the account does not exist.
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
        if (claim is null || !Guid.TryParse(claim.Value, out var id))
        {
            return null;
        }

        // A deleted account is treated as missing, so its old session cookie stops working at once.
        var account = await users.FindByIdAsync(id.ToString("D"));
        return account is { DeletedAt: null } ? account : null;
    }
}
