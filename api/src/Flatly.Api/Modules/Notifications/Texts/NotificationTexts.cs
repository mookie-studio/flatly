using Flatly.Api.Modules.Platform.Email;
using Flatly.Api.Modules.Platform.Identity;

namespace Flatly.Api.Modules.Notifications.Texts;

/// <summary>
/// The localisation catalog of notifications. Every key has a Spanish and an English template with named parameters,
/// written as <c>{name}</c>. Wording refers to tasks and dates, never to a person's failures (see #13).
/// Feature tasks add their keys here; the producer must use a key that exists.
/// </summary>
public static class NotificationTexts
{
    /// <summary>Longest text key accepted.</summary>
    public const int MaxKeyLength = 100;

    private sealed record Entry(string Spanish, string English);

    private static readonly Dictionary<string, Entry> Catalog = new(StringComparer.Ordinal)
    {
        ["chores.due_today"] = new(
            Spanish: "La tarea \"{task}\" vence hoy.",
            English: "The chore \"{task}\" is due today."),
        ["maintenance.landlord_no_reply"] = new(
            Spanish: "El casero aún no ha respondido a \"{subject}\".",
            English: "The landlord has not replied to \"{subject}\" yet."),
    };

    /// <summary>True when <paramref name="key"/> is in the catalog.</summary>
    public static bool Exists(string key) => Catalog.ContainsKey(key);

    /// <summary>
    /// Renders the text of <paramref name="key"/> in <paramref name="language"/>, replacing each <c>{name}</c> with its
    /// value from <paramref name="args"/>. Unknown or missing languages fall back to Spanish, as in the email templates.
    /// Throws <see cref="KeyNotFoundException"/> for a key that is not in the catalog.
    /// </summary>
    public static string Render(string key, string? language, IReadOnlyDictionary<string, string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!Catalog.TryGetValue(key, out var entry))
        {
            throw new KeyNotFoundException($"The notification text key '{key}' is not in the catalog.");
        }

        var template = EmailTemplates.ResolveLanguage(language) == UserLanguages.English ? entry.English : entry.Spanish;
        foreach (var (name, value) in args)
        {
            template = template.Replace($"{{{name}}}", value, StringComparison.Ordinal);
        }

        return template;
    }
}
