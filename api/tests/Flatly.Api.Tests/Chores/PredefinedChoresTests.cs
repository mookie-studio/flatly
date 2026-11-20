using Flatly.Api.Modules.Chores.Catalogue;
using Flatly.Api.Modules.Chores.Endpoints;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Platform.Identity;
using Xunit;

namespace Flatly.Api.Tests.Chores;

/// <summary>Checks the predefined chores of the catalogue (#90) and the rules for adding them, without a database.</summary>
public sealed class PredefinedChoresTests
{
    private static readonly IReadOnlyList<Guid> OneParticipant = [Guid.Parse("00000000-0000-0000-0000-00000000000a")];
    private static readonly string[] ExpectedKeys =
        ["cleaning", "trash", "bathroom", "kitchen", "shopping", "recycling", "bed_sheets", "basic_maintenance"];

    [Fact]
    public void TheCatalogueHasTheEightPredefinedChores()
    {
        Assert.Equal(
            ExpectedKeys,
            PredefinedChores.All.Select(chore => chore.Key).ToArray());
    }

    [Fact]
    public void EveryDefaultFollowsTheChoreRules()
    {
        foreach (var chore in PredefinedChores.All)
        {
            var fields = new ChoreFields(chore.NameEs, chore.RecurrenceUnit.ToString(), chore.RecurrenceEvery, chore.Weight, OneParticipant);

            Assert.Empty(ChoreRules.Check(fields, requireAll: true));
        }
    }

    [Fact]
    public void EveryChoreHasANameInBothLanguages()
    {
        foreach (var chore in PredefinedChores.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(chore.NameEs));
            Assert.False(string.IsNullOrWhiteSpace(chore.NameEn));
            Assert.NotEqual(chore.NameEs, chore.NameEn);
        }
    }

    [Theory]
    [InlineData(UserLanguages.English, "General cleaning")]
    [InlineData(UserLanguages.Spanish, "Limpieza general")]
    [InlineData("fr", "Limpieza general")]
    public void TheNameFollowsTheLanguageAndFallsBackToSpanish(string language, string expected)
    {
        var cleaning = PredefinedChores.Find("cleaning")!;

        Assert.Equal(expected, cleaning.NameFor(language));
    }

    [Fact]
    public void FindReturnsNullForAnUnknownKey()
    {
        Assert.Null(PredefinedChores.Find("ironing"));
    }

    [Fact]
    public void AValidAddRequestHasNoErrors()
    {
        var errors = new AddFromCatalogueRequestValidator().Validate(new AddFromCatalogueRequest(["cleaning", "trash"], null));

        Assert.Empty(errors);
    }

    [Fact]
    public void AddNeedsAtLeastOneKnownKeyEachAtMostOnce()
    {
        var validator = new AddFromCatalogueRequestValidator();

        Assert.Contains("keys", validator.Validate(new AddFromCatalogueRequest([], null)).Keys);
        Assert.Contains("keys", validator.Validate(new AddFromCatalogueRequest(null, null)).Keys);
        Assert.Contains("keys", validator.Validate(new AddFromCatalogueRequest(["cleaning", "ironing"], null)).Keys);
        Assert.Contains("keys", validator.Validate(new AddFromCatalogueRequest(["trash", "trash"], null)).Keys);
    }

    [Fact]
    public void ParticipantsOfAnAddAreNonEmptyAndDistinctWhenSent()
    {
        var validator = new AddFromCatalogueRequestValidator();

        Assert.Contains("participantIds", validator.Validate(new AddFromCatalogueRequest(["trash"], [])).Keys);
        Assert.Contains(
            "participantIds",
            validator.Validate(new AddFromCatalogueRequest(["trash"], [OneParticipant[0], OneParticipant[0]])).Keys);
    }
}
