namespace Flatly.Api.Modules.Notifications.Endpoints;

/// <summary>A notification as its recipient reads it.</summary>
/// <param name="Id">Identifier of the notification.</param>
/// <param name="FlatId">The flat the notification is about.</param>
/// <param name="Category">The area it comes from: <c>Flats</c>, <c>Chores</c> or <c>Maintenance</c>.</param>
/// <param name="Text">The text, rendered in the recipient's language.</param>
/// <param name="LinkTarget">A relative path inside Flatly, or null.</param>
/// <param name="CreatedAt">When the notification was created, in UTC.</param>
/// <param name="ReadAt">When the recipient read it, in UTC. Null while it is unread.</param>
public sealed record NotificationResponse(
    Guid Id,
    Guid FlatId,
    string Category,
    string Text,
    string? LinkTarget,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

/// <summary>One page of the inbox, newest first.</summary>
/// <param name="Items">The notifications of this page.</param>
/// <param name="Page">The page number, starting at 1.</param>
/// <param name="PageSize">The number of items asked for per page.</param>
/// <param name="TotalCount">How many notifications match the query, across all pages.</param>
public sealed record NotificationPageResponse(IReadOnlyList<NotificationResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>How many unread notifications the caller has.</summary>
/// <param name="Count">The number of unread notifications.</param>
public sealed record UnreadCountResponse(int Count);
