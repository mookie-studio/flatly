using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Platform.Endpoints;

/// <summary>The personal data of the signed-in member, as the export bundle holds it.</summary>
/// <param name="DisplayName">Name shown to the other members of a flat.</param>
/// <param name="Email">Email the sign-in provider reported, or null. Kept for contact only.</param>
/// <param name="AvatarUrl">Picture URL, or null.</param>
/// <param name="PreferredLanguage">Interface language, <c>es</c> or <c>en</c>.</param>
/// <param name="TimeZoneId">IANA time zone identifier.</param>
/// <param name="CreatedAt">When the account was created, in UTC.</param>
public sealed record AccountProfileExport(
    string DisplayName,
    string? Email,
    string? AvatarUrl,
    string PreferredLanguage,
    string TimeZoneId,
    DateTimeOffset CreatedAt);

/// <summary>One membership of the member, active or left, in a flat.</summary>
/// <param name="FlatId">The flat.</param>
/// <param name="FlatName">Name of the flat.</param>
/// <param name="Role"><c>Admin</c> or <c>Member</c>.</param>
/// <param name="JoinedAt">When the member joined, in UTC.</param>
/// <param name="LeftAt">When the member left, in UTC, or null while the membership is active.</param>
public sealed record AccountMembershipExport(
    Guid FlatId,
    string FlatName,
    string Role,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LeftAt);

/// <summary>The data export of the signed-in member: their profile and every membership.</summary>
/// <param name="ExportedAt">When the export was made, in UTC.</param>
/// <param name="Profile">The member's profile.</param>
/// <param name="Memberships">Every flat the member has been in, oldest first.</param>
public sealed record AccountExportResponse(
    DateTimeOffset ExportedAt,
    AccountProfileExport Profile,
    IReadOnlyList<AccountMembershipExport> Memberships);

/// <summary>
/// Endpoints for the member's own account: <c>GET account/export</c> returns their data, and <c>DELETE account</c>
/// deletes the account. Deletion leaves every flat (memberships stay as history, as with leaving), removes the sign-in
/// links so the old provider account no longer signs in, and scrubs the personal data kept on the account row.
/// </summary>
public static class AccountEndpoints
{
    /// <summary>Name shown for a deleted member in the flats they were part of.</summary>
    public const string FormerMemberName = "Former member";

    /// <summary>Maps the export and deletion endpoints under the given group.</summary>
    public static RouteGroupBuilder MapAccount(this RouteGroupBuilder group)
    {
        group.MapGet("/account/export", ExportAsync)
            .WithName("ExportAccount")
            .WithSummary("Returns the signed-in member's data: their profile and every flat membership, active or left.");

        group.MapDelete("/account", DeleteAsync)
            .WithName("DeleteAccount")
            .WithSummary(
                "Deletes the signed-in member's account. The member leaves every flat, and their name shows as a former " +
                "member there. The last admin of a flat with other members cannot delete the account (409). Requires the " +
                "antiforgery token in the X-XSRF-TOKEN header.");
        return group;
    }

    private static async Task<Results<Ok<AccountExportResponse>, UnauthorizedHttpResult>> ExportAsync(
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

        // Every flat the member has been in, active or left. This query ignores the flat filter on purpose (ADR-002);
        // the user filter keeps it to the caller's own rows.
        var rows = await database.Memberships.IgnoreQueryFilters().AsNoTracking()
            .Where(membership => membership.UserId == account.Id)
            .Join(
                database.Flats.AsNoTracking(),
                membership => membership.FlatId,
                flat => flat.Id,
                (membership, flat) => new { membership.FlatId, flat.Name, membership.Role, membership.JoinedAt, membership.LeftAt })
            .OrderBy(row => row.JoinedAt).ThenBy(row => row.FlatId)
            .ToListAsync(cancellationToken);

        var memberships = rows
            .Select(row => new AccountMembershipExport(row.FlatId, row.Name, row.Role.ToString(), row.JoinedAt, row.LeftAt))
            .ToList();
        var profile = new AccountProfileExport(
            account.DisplayName,
            account.Email,
            account.AvatarUrl,
            account.PreferredLanguage,
            account.TimeZoneId,
            account.CreatedAt);

        return TypedResults.Ok(new AccountExportResponse(clock.GetUtcNow(), profile, memberships));
    }

    private static async Task<Results<NoContent, BadRequest, ProblemHttpResult, UnauthorizedHttpResult>> DeleteAsync(
        ClaimsPrincipal principal,
        HttpContext httpContext,
        IAntiforgery antiforgery,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        UserManager<FlatlyUser> users,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        // Deletion is a state change, so it validates the antiforgery token like sign-out does.
        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            return TypedResults.BadRequest();
        }

        var activeMemberships = await database.Memberships.IgnoreQueryFilters()
            .Where(membership => membership.UserId == account.Id && membership.LeftAt == null)
            .ToListAsync(cancellationToken);

        // The last-admin rule of leaving a flat (see LeaveFlatEndpoint): a flat with other members must keep an admin.
        var blockedFlatIds = new List<Guid>();
        foreach (var membership in activeMemberships.Where(item => item.Role == MemberRole.Admin))
        {
            var flatId = membership.FlatId;
            var otherMembers = await database.Memberships.IgnoreQueryFilters().AnyAsync(
                item => item.FlatId == flatId && item.UserId != account.Id && item.LeftAt == null,
                cancellationToken);
            var otherAdmins = await database.Memberships.IgnoreQueryFilters().AnyAsync(
                item => item.FlatId == flatId && item.UserId != account.Id && item.LeftAt == null && item.Role == MemberRole.Admin,
                cancellationToken);
            if (otherMembers && !otherAdmins)
            {
                blockedFlatIds.Add(flatId);
            }
        }

        if (blockedFlatIds.Count > 0)
        {
            var flatNames = await database.Flats.AsNoTracking()
                .Where(flat => blockedFlatIds.Contains(flat.Id))
                .OrderBy(flat => flat.Name)
                .Select(flat => flat.Name)
                .ToListAsync(cancellationToken);
            return TypedResults.Problem(
                title: "Last admin of a flat",
                detail: "Make another member an admin of these flats before deleting your account: " + string.Join(", ", flatNames) + ".",
                statusCode: StatusCodes.Status409Conflict);
        }

        var now = clock.GetUtcNow();
        // Everything in one transaction, so a failure never leaves a half-deleted account.
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        foreach (var membership in activeMemberships)
        {
            membership.LeftAt = now;
        }

        // Sign-in links and tokens go, so the provider account no longer reaches this account.
        database.UserLogins.RemoveRange(await database.UserLogins.Where(item => item.UserId == account.Id).ToListAsync(cancellationToken));
        database.UserClaims.RemoveRange(await database.UserClaims.Where(item => item.UserId == account.Id).ToListAsync(cancellationToken));
        database.UserTokens.RemoveRange(await database.UserTokens.Where(item => item.UserId == account.Id).ToListAsync(cancellationToken));
        await database.SaveChangesAsync(cancellationToken);

        // The row stays, because memberships point at it. Its personal data is replaced or removed.
        account.DisplayName = FormerMemberName;
        account.AvatarUrl = null;
        account.DeletedAt = now;
        ThrowIfFailed(await users.SetUserNameAsync(account, $"deleted-{account.Id:N}"));
        ThrowIfFailed(await users.SetEmailAsync(account, null));
        ThrowIfFailed(await users.UpdateSecurityStampAsync(account));
        ThrowIfFailed(await users.UpdateAsync(account));

        await transaction.CommitAsync(cancellationToken);
        await httpContext.SignOutAsync(SessionAuthentication.Scheme);
        return TypedResults.NoContent();
    }

    private static void ThrowIfFailed(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
