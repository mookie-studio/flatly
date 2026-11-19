using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Endpoints;

/// <summary>Maps <c>GET /</c>: the caller's inbox, newest first, paged, optionally only the unread ones.</summary>
public static class ListNotificationsEndpoint
{
    /// <summary>Largest page size accepted.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page size when none is given.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapListNotifications(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListAsync)
            .WithName("ListNotifications")
            .WithSummary("Lists the signed-in account's notifications, newest first. Use unread=true for the unread ones only.");
        return group;
    }

    private static async Task<Results<Ok<NotificationPageResponse>, ValidationProblem, UnauthorizedHttpResult>> ListAsync(
        bool? unread,
        int? page,
        int? pageSize,
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

        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? DefaultPageSize;
        var errors = new Dictionary<string, string[]>();
        if (currentPage < 1)
        {
            errors["page"] = ["Page must be 1 or greater."];
        }

        if (currentPageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = NotificationQueries.VisibleTo(database, account.Id).AsNoTracking();
        if (unread == true)
        {
            query = query.Where(notification => notification.ReadAt == null);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(notification => notification.CreatedAt).ThenByDescending(notification => notification.Id)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToListAsync(cancellationToken);

        IReadOnlyList<NotificationResponse> items = rows
            .Select(notification => NotificationQueries.ToResponse(notification, account.PreferredLanguage))
            .ToList();
        return TypedResults.Ok(new NotificationPageResponse(items, currentPage, currentPageSize, total));
    }
}
