using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Modules.Flats.Validation;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>Request to rename a flat.</summary>
/// <param name="Name">Required, at most 80 characters.</param>
public sealed record UpdateFlatRequest(string? Name);

/// <summary>Maps <c>PATCH /{flatId}</c>: renames a flat. Only its admins may rename it.</summary>
public static class RenameFlatEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapRenameFlat(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPatch("/", RenameAsync)
            .AddEndpointFilter<ValidationFilter<UpdateFlatRequest>>()
            .WithName("RenameFlat")
            .WithSummary("Renames a flat. Only an admin of the flat may do it; members get 403.");
        return group;
    }

    private static async Task<Results<Ok<FlatResponse>, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> RenameAsync(
        Guid flatId,
        UpdateFlatRequest request,
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

        // The flat route has already checked that the caller is an active member, so this finds the caller's membership.
        var role = await database.Memberships.AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.UserId == account.Id && membership.LeftAt == null)
            .Select(membership => (MemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (role is null)
        {
            return TypedResults.NotFound();
        }

        if (role != MemberRole.Admin)
        {
            return TypedResults.Forbid();
        }

        var flat = await database.Flats.SingleOrDefaultAsync(item => item.Id == flatId, cancellationToken);
        if (flat is null)
        {
            return TypedResults.NotFound();
        }

        // The filter has already checked the request, so the name is present.
        flat.Name = request.Name!.Trim();
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new FlatResponse(flat.Id, flat.Name, flat.CreatedAt, MemberRole.Admin.ToString()));
    }
}
