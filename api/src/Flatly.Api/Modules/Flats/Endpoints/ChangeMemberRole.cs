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

/// <summary>Request to change a member's role in a flat.</summary>
/// <param name="Role">Required: <c>Admin</c> or <c>Member</c>.</param>
public sealed record ChangeMemberRoleRequest(string? Role);

/// <summary>
/// Maps <c>PATCH /{flatId}/members/{userId}</c>: an admin promotes or demotes a member. A flat always keeps at least
/// one admin, so the sole admin cannot be demoted until another member is promoted.
/// </summary>
public static class ChangeMemberRoleEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapChangeMemberRole(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPatch("/members/{userId:guid}", ChangeRoleAsync)
            .AddEndpointFilter<ValidationFilter<ChangeMemberRoleRequest>>()
            .WithName("ChangeFlatMemberRole")
            .WithSummary(
                "Promotes or demotes an active member of a flat. Only an admin may do it; members get 403. " +
                "The sole admin of a flat cannot be demoted, so the flat always keeps an admin (409).");
        return group;
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult, ProblemHttpResult, UnauthorizedHttpResult>> ChangeRoleAsync(
        Guid flatId,
        Guid userId,
        ChangeMemberRoleRequest request,
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

        var membership = await database.Memberships
            .SingleOrDefaultAsync(item => item.FlatId == flatId && item.UserId == userId && item.LeftAt == null, cancellationToken);
        if (membership is null)
        {
            return TypedResults.NotFound();
        }

        // The filter has already checked the request, so the role is Admin or Member.
        var newRole = request.Role == nameof(MemberRole.Admin) ? MemberRole.Admin : MemberRole.Member;
        if (membership.Role == newRole)
        {
            return TypedResults.NoContent();
        }

        if (newRole == MemberRole.Member)
        {
            var otherAdmins = await database.Memberships.AnyAsync(
                item => item.FlatId == flatId && item.UserId != userId && item.LeftAt == null && item.Role == MemberRole.Admin,
                cancellationToken);
            if (!otherAdmins)
            {
                return TypedResults.Problem(
                    title: "The last admin cannot be demoted.",
                    detail: "A flat must keep an admin. Promote another member to admin before demoting this one.",
                    statusCode: StatusCodes.Status409Conflict);
            }
        }

        membership.Role = newRole;
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}
