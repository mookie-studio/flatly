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
/// Maps <c>DELETE /{flatId}/members/{userId}</c>: an admin removes another member from the flat. Like leaving, the
/// membership is kept as history with <c>left_at</c> set, so the member's past contributions stay attributed to them.
/// </summary>
public static class RemoveMemberEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapRemoveMember(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapDelete("/members/{userId:guid}", RemoveAsync)
            .WithName("RemoveFlatMember")
            .WithSummary(
                "Removes a member from a flat. Only an admin may do it; members get 403. Admins cannot remove " +
                "themselves: they use the leave endpoint. Other flats and non-members answer 404.");
        return group;
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, BadRequest, UnauthorizedHttpResult>> RemoveAsync(
        Guid flatId,
        Guid userId,
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
        var callerRole = await database.Memberships.AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.UserId == account.Id && membership.LeftAt == null)
            .Select(membership => (MemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (callerRole is null)
        {
            return TypedResults.NotFound();
        }

        if (callerRole != MemberRole.Admin)
        {
            return TypedResults.Forbid();
        }

        if (userId == account.Id)
        {
            return TypedResults.BadRequest();
        }

        var membership = await database.Memberships
            .SingleOrDefaultAsync(item => item.FlatId == flatId && item.UserId == userId && item.LeftAt == null, cancellationToken);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        membership.LeftAt = clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
