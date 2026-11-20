using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Invites;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>Request to join a flat with its invite code.</summary>
/// <param name="Code">The invite code. Required; case-insensitive and ignores surrounding spaces.</param>
public sealed record JoinFlatRequest(string? Code);

/// <summary>
/// Maps <c>POST /join</c>: the signed-in account joins the flat that has the given invite code as a member.
/// Joining is idempotent: an account that is already an active member gets the flat back and nothing changes.
/// Attempts are rate-limited per account (see <see cref="JoinFlatEndpoint.RateLimitPolicy"/>).
/// </summary>
public static class JoinFlatEndpoint
{
    /// <summary>Name of the rate-limiting policy that throttles join attempts.</summary>
    public const string RateLimitPolicy = "join-flat";

    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapJoinFlat(this RouteGroupBuilder group)
    {
        group.MapPost("/join", JoinAsync)
            .AddEndpointFilter<ValidationFilter<JoinFlatRequest>>()
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("JoinFlat")
            .WithSummary(
                "Joins a flat with its invite code as a Member. Joining again while still a member changes nothing. " +
                "An unknown code answers 404 and an expired code answers 410. Attempts are rate-limited per account and answer 429.");
        return group;
    }

    private static async Task<Results<Ok<FlatResponse>, ProblemHttpResult, UnauthorizedHttpResult>> JoinAsync(
        JoinFlatRequest request,
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

        var code = InviteCodes.Normalize(request.Code ?? string.Empty);
        var flat = await database.Flats.AsNoTracking()
            .SingleOrDefaultAsync(item => item.InviteCode == code, cancellationToken);
        if (flat is null)
        {
            return TypedResults.Problem(
                title: "Invite code not found.",
                detail: "No flat has this invite code. Check the code and try again.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var now = clock.GetUtcNow();
        if (flat.InviteCodeExpiresAt is not { } expiresAt || expiresAt <= now)
        {
            return TypedResults.Problem(
                title: "Invite code has expired.",
                detail: "Ask an admin of the flat for a new invite code.",
                statusCode: StatusCodes.Status410Gone);
        }

        var existingRole = await FindActiveRoleAsync(database, flat.Id, account.Id, cancellationToken);
        if (existingRole is not null)
        {
            return TypedResults.Ok(ToResponse(flat, existingRole.Value));
        }

        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flat.Id,
            UserId = account.Id,
            Role = MemberRole.Member,
            JoinedAt = now,
        });
        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent join by the same account already created the active membership, which the unique index
            // allows only once. That is the same outcome as joining twice, so it is not an error.
            database.ChangeTracker.Clear();
            var joinedRole = await FindActiveRoleAsync(database, flat.Id, account.Id, cancellationToken);
            if (joinedRole is null)
            {
                throw;
            }

            return TypedResults.Ok(ToResponse(flat, joinedRole.Value));
        }

        return TypedResults.Ok(ToResponse(flat, MemberRole.Member));
    }

    /// <summary>The caller's role in the flat, or null when the caller has no active membership there.</summary>
    /// <remarks>
    /// The flat route is not used here, so no flat is in scope and the flat filter would hide every membership. The
    /// query filters on the flat and the user itself instead.
    /// </remarks>
    private static Task<MemberRole?> FindActiveRoleAsync(
        FlatlyDbContext database,
        Guid flatId,
        Guid userId,
        CancellationToken cancellationToken) =>
        database.Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(membership => membership.FlatId == flatId && membership.UserId == userId && membership.LeftAt == null)
            .Select(membership => (MemberRole?)membership.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private static FlatResponse ToResponse(Flat flat, MemberRole role) =>
        new(flat.Id, flat.Name, flat.CreatedAt, role.ToString());
}
