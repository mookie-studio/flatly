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

/// <summary>Maps <c>GET /{flatId}/members</c>: the active members of a flat, for its active members only.</summary>
public static class ListMembersEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapListMembers(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapGet("/members", ListAsync)
            .WithName("ListFlatMembers")
            .WithSummary("Lists the active members of a flat, admins first. Members who have left are not listed.");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<FlatMemberResponse>>, NotFound, UnauthorizedHttpResult>> ListAsync(
        Guid flatId,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        // The flat route has already checked that the caller is an active member, so the flat filter on memberships
        // limits this query to the flat in scope.
        var rows = await database.Memberships.AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.LeftAt == null)
            .Join(
                database.Users.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    membership.UserId,
                    user.DisplayName,
                    user.AvatarUrl,
                    membership.Role,
                    membership.JoinedAt,
                })
            .ToListAsync(cancellationToken);

        IReadOnlyList<FlatMemberResponse> members = rows
            .OrderBy(row => row.Role)
            .ThenBy(row => row.JoinedAt)
            .ThenBy(row => row.DisplayName)
            .ThenBy(row => row.UserId)
            .Select(row => new FlatMemberResponse(row.UserId, row.DisplayName, row.AvatarUrl, row.Role.ToString(), row.JoinedAt))
            .ToList();
        return TypedResults.Ok(members);
    }
}
