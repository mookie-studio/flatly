using Flatly.Api.Modules.Notifications.Texts;
using Xunit;

namespace Flatly.Api.Tests.Notifications;

public sealed class NotificationTextsTests
{
    private static readonly Dictionary<string, string> BathroomChore = new() { ["task"] = "Bathroom" };

    [Fact]
    public void TheTextIsRenderedInTheRequestedLanguage()
    {
        Assert.Equal("La tarea \"Bathroom\" vence hoy.", NotificationTexts.Render("chores.due_today", "es", BathroomChore));
        Assert.Equal("The chore \"Bathroom\" is due today.", NotificationTexts.Render("chores.due_today", "en", BathroomChore));
    }

    [Fact]
    public void AnUnknownOrMissingLanguageFallsBackToSpanish()
    {
        Assert.Equal("La tarea \"Bathroom\" vence hoy.", NotificationTexts.Render("chores.due_today", "fr", BathroomChore));
        Assert.Equal("La tarea \"Bathroom\" vence hoy.", NotificationTexts.Render("chores.due_today", null, BathroomChore));
    }

    [Fact]
    public void ParametersThatAreNotGivenStayAsThePlaceholder()
    {
        Assert.Equal("The chore \"{task}\" is due today.", NotificationTexts.Render("chores.due_today", "en", new Dictionary<string, string>()));
    }

    [Fact]
    public void AKeyOutsideTheCatalogIsRejected()
    {
        Assert.False(NotificationTexts.Exists("chores.unknown"));
        Assert.Throws<KeyNotFoundException>(() => NotificationTexts.Render("chores.unknown", "en", BathroomChore));
    }
}
