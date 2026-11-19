using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>Maps <c>GET /{flatId}</c>: one flat, for its active members only.</summary>
public static class GetFlatEndpoint
{
    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapGetFlat(this RouteGroupBuilder group)
    {
        group.MapGet("/{flatId:guid}", GetAsync)
            .WithName("GetFlat")
            .WithSummary("Gets a flat the signed-in account is an active member of. Other flats answer 404.");
        return group;
    }

    private static async Task<Results<Ok<FlatResponse>, NotFound, UnauthorizedHttpResult>> GetAsync(
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

        // "Not found" for a flat the caller does not belong to, so the existence of other flats is not revealed (ADR-002).
        var row = await database.Memberships.AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.UserId == account.Id && membership.LeftAt == null)
            .Join(
                database.Flats.AsNoTracking(),
                membership => membership.FlatId,
                flat => flat.Id,
                (membership, flat) => new { flat.Id, flat.Name, flat.CreatedAt, membership.Role })
            .SingleOrDefaultAsync(cancellationToken);
        if (row is null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new FlatResponse(row.Id, row.Name, row.CreatedAt, row.Role.ToString()));
    }
}
