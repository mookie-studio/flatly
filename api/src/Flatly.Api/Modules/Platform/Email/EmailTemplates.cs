using Flatly.Api.Modules.Platform.Identity;

namespace Flatly.Api.Modules.Platform.Email;

/// <summary>The content of a notification email, before it is rendered in a language.</summary>
/// <param name="Title">Short, task-focused title. It becomes the subject line after "Flatly: ".</param>
/// <param name="Body">One or two sentences saying what happened and what the member can do.</param>
/// <param name="ActionUrl">Link to the item in Flatly, when there is one.</param>
public sealed record NotificationContent(string Title, string Body, string? ActionUrl = null);

/// <summary>Renders notification emails in the recipient's language. Wording is neutral and task-focused.</summary>
public static class EmailTemplates
{
    /// <summary>
    /// Returns the language to render in. Only Spanish and English exist; anything else falls back to Spanish,
    /// the default language of the product.
    /// </summary>
    public static string ResolveLanguage(string? language) =>
        language == UserLanguages.English ? UserLanguages.English : UserLanguages.Spanish;

    /// <summary>Renders a notification to <paramref name="to"/> in the given language.</summary>
    public static EmailMessage Notification(string to, string? language, NotificationContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var texts = ResolveLanguage(language) == UserLanguages.English ? English : Spanish;

        var lines = new List<string> { content.Body, string.Empty };
        if (content.ActionUrl is not null)
        {
            lines.Add($"{texts.OpenLabel} {content.ActionUrl}");
            lines.Add(string.Empty);
        }
        lines.Add("--");
        lines.Add(texts.Footer);

        return new EmailMessage(to, $"Flatly: {content.Title}", string.Join('\n', lines));
    }

    private sealed record Texts(string OpenLabel, string Footer);

    private static readonly Texts Spanish = new(
        OpenLabel: "Ver en Flatly:",
        Footer: "Recibes este aviso porque formas parte de un piso en Flatly. Puedes cambiar tus avisos desde tu perfil.");

    private static readonly Texts English = new(
        OpenLabel: "View in Flatly:",
        Footer: "You receive this notice because you are a member of a flat in Flatly. You can change your notices from your profile.");
}
