namespace Flatly.Api.Modules.Notifications.Domain;

/// <summary>
/// An in-app notification for one member of a flat. The text is not stored as words: <see cref="TextKey"/> names an entry
/// of the localisation catalog and <see cref="TextArgsJson"/> holds its parameters, so each member reads it in their own language.
/// </summary>
public sealed class Notification
{
    /// <summary>Identifier of the notification.</summary>
    public Guid Id { get; set; }

    /// <summary>The member who receives the notification.</summary>
    public Guid UserId { get; set; }

    /// <summary>The flat the notification is about. Only shown while the recipient is an active member of it.</summary>
    public Guid FlatId { get; set; }

    /// <summary>The area the notification comes from.</summary>
    public NotificationCategory Category { get; set; }

    /// <summary>Key of the localisation catalog entry, for example <c>chores.due_today</c>.</summary>
    public string TextKey { get; set; } = string.Empty;

    /// <summary>JSON object with the parameters of the text, for example <c>{"task":"Bathroom"}</c>.</summary>
    public string TextArgsJson { get; set; } = "{}";

    /// <summary>Relative path inside Flatly the notification links to, for example <c>/chores/42</c>. Null when there is none.</summary>
    public string? LinkTarget { get; set; }

    /// <summary>When the member read the notification, in UTC. Null while it is unread.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>When the notification was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
