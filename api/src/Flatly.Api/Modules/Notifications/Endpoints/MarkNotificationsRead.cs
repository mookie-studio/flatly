using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Endpoints;

/// <summary>Maps <c>POST /{notificationId}/read</c> and <c>POST /read-all</c>: marks the caller's notifications as read.</summary>
public static class MarkNotificationsReadEndpoints
{
    /// <summary>Maps the endpoints under the given group.</summary>
    public static RouteGroupBuilder MapMarkNotificationsRead(this RouteGroupBuilder group)
    {
        group.MapPost("/{notificationId:guid}/read", MarkOneAsync)
            .WithName("MarkNotificationRead")
            .WithSummary("Marks one of the signed-in account's notifications as read. Marking it again changes nothing.");
        group.MapPost("/read-all", MarkAllAsync)
            .WithName("MarkAllNotificationsRead")
            .WithSummary("Marks every unread notification of the signed-in account as read.");
        return group;
    }

    private static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> MarkOneAsync(
        Guid notificationId,
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

        // Another member's notification answers 404, the same as a missing one, so no notification's existence is revealed.
        var notification = await NotificationQueries.VisibleTo(database, account.Id)
            .SingleOrDefaultAsync(item => item.Id == notificationId, cancellationToken);
        if (notification is null)
        {
            return TypedResults.NotFound();
        }

        notification.ReadAt ??= clock.GetUtcNow();
        await database.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult>> MarkAllAsync(
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

        var now = clock.GetUtcNow();
        await NotificationQueries.VisibleTo(database, account.Id)
            .Where(notification => notification.ReadAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(notification => notification.ReadAt, now), cancellationToken);
        return TypedResults.NoContent();
    }
}
