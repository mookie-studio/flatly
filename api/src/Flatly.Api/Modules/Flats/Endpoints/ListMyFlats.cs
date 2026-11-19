using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>Maps <c>GET /</c>: the flats the signed-in account is an active member of.</summary>
public static class ListMyFlatsEndpoint
{
    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapListMyFlats(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListMyFlats")
            .WithSummary("Lists the flats the signed-in account is a member of, oldest first.");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<FlatResponse>>, UnauthorizedHttpResult>> ListAsync(
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

        // The caller's memberships in every flat, so this is the one query that ignores the flat filter on purpose (ADR-002).
        // The user filter keeps it to the caller's own rows.
        var rows = await database.Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(membership => membership.UserId == account.Id && membership.LeftAt == null)
            .Join(
                database.Flats.AsNoTracking(),
                membership => membership.FlatId,
                flat => flat.Id,
                (membership, flat) => new { flat.Id, flat.Name, flat.CreatedAt, membership.Role })
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Name).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken);

        IReadOnlyList<FlatResponse> flats = rows
            .Select(row => new FlatResponse(row.Id, row.Name, row.CreatedAt, row.Role.ToString()))
            .ToList();
        return TypedResults.Ok(flats);
    }
}
