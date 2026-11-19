using System.Text.Json;
using Flatly.Api.Data;
using Flatly.Api.Modules.Notifications.Data;
using Flatly.Api.Modules.Notifications.Domain;
using Flatly.Api.Modules.Notifications.Texts;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Notifications.Services;

/// <summary>
/// Creates notifications for other modules. Internal: no endpoint calls it. A module calls
/// <see cref="NotifyNowAsync"/> when something happens; scheduled reminders reuse it when they are due (#13).
/// </summary>
public interface INotificationService
{
    /// <summary>Creates one notification per recipient, now. Throws when a recipient is not an active member of the flat.</summary>
    Task NotifyNowAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>What a module wants to tell a set of members of one flat.</summary>
/// <param name="FlatId">The flat the notification is about.</param>
/// <param name="RecipientIds">The members to notify. Duplicates are ignored.</param>
/// <param name="Category">The area the notification comes from.</param>
/// <param name="TextKey">A key of the localisation catalog (see <see cref="NotificationTexts"/>).</param>
/// <param name="Args">The parameters of the text. Null means none.</param>
/// <param name="LinkTarget">A relative path inside Flatly, or null.</param>
public sealed record NotificationRequest(
    Guid FlatId,
    IReadOnlyCollection<Guid> RecipientIds,
    NotificationCategory Category,
    string TextKey,
    IReadOnlyDictionary<string, string>? Args = null,
    string? LinkTarget = null);

/// <summary>Writes notifications in one save, so either all recipients get theirs or none does.</summary>
public sealed class NotificationService(FlatlyDbContext database, TimeProvider clock) : INotificationService
{
    /// <inheritdoc />
    public async Task NotifyNowAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RecipientIds);
        if (!NotificationTexts.Exists(request.TextKey))
        {
            throw new ArgumentException($"The notification text key '{request.TextKey}' is not in the catalog.", nameof(request));
        }

        if (request.LinkTarget is { Length: > NotificationsModelConfiguration.MaxLinkTargetLength })
        {
            throw new ArgumentException("The link target is too long.", nameof(request));
        }

        var recipients = request.RecipientIds.Distinct().ToList();
        if (recipients.Count == 0)
        {
            return;
        }

        // Notifying a former member or a stranger would leak the flat's activity to them, so every recipient must be active.
        var activeCount = await database.Memberships.AsNoTracking()
            .CountAsync(
                membership => membership.FlatId == request.FlatId
                    && membership.LeftAt == null
                    && recipients.Contains(membership.UserId),
                cancellationToken);
        if (activeCount != recipients.Count)
        {
            throw new InvalidOperationException("Every recipient must be an active member of the flat.");
        }

        var argsJson = JsonSerializer.Serialize(request.Args ?? new Dictionary<string, string>());
        var now = clock.GetUtcNow();
        foreach (var recipient in recipients)
        {
            database.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = recipient,
                FlatId = request.FlatId,
                Category = request.Category,
                TextKey = request.TextKey,
                TextArgsJson = argsJson,
                LinkTarget = request.LinkTarget,
                CreatedAt = now,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
