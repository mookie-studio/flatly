using Flatly.Api.Modules.Platform.Email;
using Xunit;

namespace Flatly.Api.Tests.Platform.Email;

public class EmailTemplatesTests
{
    private static readonly NotificationContent Content = new(
        Title: "Tarea pendiente",
        Body: "Ana te ha asignado la limpieza de la cocina.",
        ActionUrl: "https://flatly.example/tasks/42");

    [Theory]
    [InlineData("en", "en")]
    [InlineData("es", "es")]
    [InlineData("fr", "es")]
    [InlineData(null, "es")]
    public void ResolveLanguageFallsBackToSpanish(string? language, string expected)
    {
        Assert.Equal(expected, EmailTemplates.ResolveLanguage(language));
    }

    [Fact]
    public void SpanishNotificationUsesSpanishWording()
    {
        var message = EmailTemplates.Notification("ana@example.com", "es", Content);

        Assert.Equal("ana@example.com", message.To);
        Assert.Equal("Flatly: Tarea pendiente", message.Subject);
        Assert.Contains("Ana te ha asignado la limpieza de la cocina.", message.TextBody);
        Assert.Contains("Ver en Flatly: https://flatly.example/tasks/42", message.TextBody);
        Assert.Contains("Recibes este aviso", message.TextBody);
    }

    [Fact]
    public void EnglishNotificationUsesEnglishWording()
    {
        var message = EmailTemplates.Notification("ana@example.com", "en", Content);

        Assert.Equal("Flatly: Tarea pendiente", message.Subject);
        Assert.Contains("View in Flatly: https://flatly.example/tasks/42", message.TextBody);
        Assert.Contains("You receive this notice", message.TextBody);
        Assert.DoesNotContain("Recibes", message.TextBody);
    }

    [Fact]
    public void NotificationWithoutActionOmitsTheLink()
    {
        var message = EmailTemplates.Notification(
            "ana@example.com",
            "en",
            new NotificationContent("Reminder", "The bins go out tonight."));

        Assert.DoesNotContain("View in Flatly", message.TextBody);
        Assert.Contains("The bins go out tonight.", message.TextBody);
    }
}
