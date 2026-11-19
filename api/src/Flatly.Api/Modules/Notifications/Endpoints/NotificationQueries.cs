using System.Text.Json;
using Flatly.Api.Data;
using Flatly.Api.Modules.Notifications.Domain;
using Flatly.Api.Modules.Notifications.Texts;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Endpoints;

/// <summary>Queries shared by the notification endpoints. Every query is limited to the caller's own notifications.</summary>
internal static class NotificationQueries
{
    /// <summary>
    /// The notifications the caller may see: their own, about a flat they are still an active member of. Notifications about
    /// a flat they left stay in the table but disappear from the inbox (ADR-002). The membership check ignores the flat
    /// filter on purpose: the inbox spans every flat of the member, while the request has no current flat.
    /// </summary>
    public static IQueryable<Notification> VisibleTo(FlatlyDbContext database, Guid userId) =>
        database.Notifications.Where(notification => notification.UserId == userId
            && database.Memberships.IgnoreQueryFilters().Any(membership => membership.FlatId == notification.FlatId
                && membership.UserId == userId
                && membership.LeftAt == null));

    /// <summary>Turns a stored notification into the response, rendering its text in the reader's language.</summary>
    public static NotificationResponse ToResponse(Notification notification, string? language)
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, string>>(notification.TextArgsJson) ?? [];
        return new NotificationResponse(
            notification.Id,
            notification.FlatId,
            notification.Category.ToString(),
            NotificationTexts.Render(notification.TextKey, language, args),
            notification.LinkTarget,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
