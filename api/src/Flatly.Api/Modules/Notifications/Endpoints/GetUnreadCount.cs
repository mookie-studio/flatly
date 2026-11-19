using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Endpoints;

/// <summary>Maps <c>GET /unread-count</c>: how many notifications the caller has not read, for the inbox badge.</summary>
public static class GetUnreadCountEndpoint
{
    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapGetUnreadCount(this RouteGroupBuilder group)
    {
        group.MapGet("/unread-count", GetAsync)
            .WithName("GetUnreadNotificationCount")
            .WithSummary("Counts the signed-in account's unread notifications.");
        return group;
    }

    private static async Task<Results<Ok<UnreadCountResponse>, UnauthorizedHttpResult>> GetAsync(
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

        var count = await NotificationQueries.VisibleTo(database, account.Id)
            .CountAsync(notification => notification.ReadAt == null, cancellationToken);
        return TypedResults.Ok(new UnreadCountResponse(count));
    }
}
