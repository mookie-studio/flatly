using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Invites;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>A flat's invite code and the moment it stops working.</summary>
/// <param name="Code">The code, in upper case.</param>
/// <param name="ExpiresAt">When the code stops working, in UTC.</param>
public sealed record InviteCodeResponse(string Code, DateTimeOffset ExpiresAt);

/// <summary>
/// Maps the invite code endpoints: <c>GET /{flatId}/invite-code</c> reads the current code, and
/// <c>POST /{flatId}/invite-code/regenerate</c> replaces it. Both are for admins only.
/// </summary>
public static class InviteCodeEndpoints
{
    /// <summary>Maps the endpoints under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapInviteCode(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapGet("/invite-code", ReadAsync)
            .WithName("GetFlatInviteCode")
            .WithSummary("Gets the invite code of a flat and when it expires. Only an admin may read it; members get 403.");
        group.MapFlatScope().MapPost("/invite-code/regenerate", RegenerateAsync)
            .WithName("RegenerateFlatInviteCode")
            .WithSummary(
                "Replaces the invite code of a flat with a new one that lasts seven days. The previous code stops working. " +
                "Only an admin may do it; members get 403.");
        return group;
    }

    private static async Task<Results<Ok<InviteCodeResponse>, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> ReadAsync(
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

        var callerRole = await FindCallerRoleAsync(database, flatId, account.Id, cancellationToken);
        if (callerRole is null)
        {
            return TypedResults.NotFound();
        }

        if (callerRole != MemberRole.Admin)
        {
            return TypedResults.Forbid();
        }

        var flat = await database.Flats.SingleOrDefaultAsync(item => item.Id == flatId, cancellationToken);
        if (flat is null)
        {
            return TypedResults.NotFound();
        }

        var code = flat.InviteCode;
        var expiresAt = flat.InviteCodeExpiresAt;
        if (code is null || expiresAt is null)
        {
            // Flats created before invite codes existed get their first code the first time an admin asks for it.
            code = await InviteCodes.GenerateUniqueAsync(database, cancellationToken);
            expiresAt = clock.GetUtcNow() + InviteCodes.Lifetime;
            flat.InviteCode = code;
            flat.InviteCodeExpiresAt = expiresAt;
            await database.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(new InviteCodeResponse(code, expiresAt.Value));
    }

    private static async Task<Results<Ok<InviteCodeResponse>, NotFound, ForbidHttpResult, UnauthorizedHttpResult>> RegenerateAsync(
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

        var callerRole = await FindCallerRoleAsync(database, flatId, account.Id, cancellationToken);
        if (callerRole is null)
        {
            return TypedResults.NotFound();
        }

        if (callerRole != MemberRole.Admin)
        {
            return TypedResults.Forbid();
        }

        var flat = await database.Flats.SingleOrDefaultAsync(item => item.Id == flatId, cancellationToken);
        if (flat is null)
        {
            return TypedResults.NotFound();
        }

        // Replacing the code in the same row invalidates the previous one: the old value is no longer stored.
        var code = await InviteCodes.GenerateUniqueAsync(database, cancellationToken);
        var expiresAt = clock.GetUtcNow() + InviteCodes.Lifetime;
        flat.InviteCode = code;
        flat.InviteCodeExpiresAt = expiresAt;
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(new InviteCodeResponse(code, expiresAt));
    }

    /// <summary>The caller's role in the flat, or null when the caller has no active membership there.</summary>
    private static Task<MemberRole?> FindCallerRoleAsync(
        FlatlyDbContext database,
        Guid flatId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.Memberships.AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.UserId == userId && membership.LeftAt == null)
            .Select(membership => (MemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);
}
