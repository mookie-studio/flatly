using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>
/// Maps <c>POST /{flatId}/leave</c>: the signed-in member leaves the flat. The membership is kept as history with
/// <c>left_at</c> set (ADR-002), so the flat's records still show who was a member.
/// </summary>
public static class LeaveFlatEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapLeaveFlat(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPost("/leave", LeaveAsync)
            .WithName("LeaveFlat")
            .WithSummary(
                "Leaves a flat. The membership is kept as history. The last admin of a flat with other members " +
                "cannot leave, so the flat always keeps an admin.");
        return group;
    }

    private static async Task<Results<NoContent, NotFound, Conflict, UnauthorizedHttpResult>> LeaveAsync(
        Guid flatId,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        // The flat route has already checked that the caller is an active member, so this finds the caller's membership.
        var membership = await database.Memberships
            .SingleOrDefaultAsync(item => item.FlatId == flatId && item.UserId == account.Id && item.LeftAt == null, cancellationToken);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        if (membership.Role == MemberRole.Admin)
        {
            var otherMembers = await database.Memberships.AnyAsync(
                item => item.FlatId == flatId && item.UserId != account.Id && item.LeftAt == null,
                cancellationToken);
            var otherAdmins = await database.Memberships.AnyAsync(
                item => item.FlatId == flatId && item.UserId != account.Id && item.LeftAt == null && item.Role == MemberRole.Admin,
                cancellationToken);
            if (otherMembers && !otherAdmins)
            {
                return TypedResults.Conflict();
            }
        }

        membership.LeftAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
