using Flatly.Api.Modules.Chores.Validation;
using Xunit;

namespace Flatly.Api.Tests.Chores;

/// <summary>Table-style checks of the chore rules, without a server or a database.</summary>
public sealed class ChoreRulesTests
{
    private static readonly IReadOnlyList<Guid> OneParticipant = [Guid.Parse("00000000-0000-0000-0000-00000000000a")];

    private static ChoreFields ValidFields() => new("Bañera", "Week", 2, 4, OneParticipant);

    [Fact]
    public void ACompleteChoreIsValid()
    {
        Assert.Empty(ChoreRules.Check(ValidFields(), requireAll: true));
    }

    [Fact]
    public void CreateRequiresEveryField()
    {
        var errors = ChoreRules.Check(new ChoreFields(null, null, null, null, null), requireAll: true);

        Assert.Equal(
            new[] { "name", "participantIds", "recurrenceEvery", "recurrenceUnit", "weight" },
            errors.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void UpdateChecksOnlyTheFieldsThatAreSent()
    {
        Assert.Empty(ChoreRules.Check(new ChoreFields(null, null, null, null, null), requireAll: false));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankNameIsRejected(string name)
    {
        var errors = ChoreRules.Check(ValidFields() with { Name = name }, requireAll: true);

        Assert.Contains("name", errors.Keys);
    }

    [Fact]
    public void ANameOfEightyCharactersIsValidAndEightyOneIsNot()
    {
        Assert.Empty(ChoreRules.Check(ValidFields() with { Name = new string('x', 80) }, requireAll: true));
        Assert.Contains("name", ChoreRules.Check(ValidFields() with { Name = new string('x', 81) }, requireAll: true).Keys);
    }

    [Theory]
    [InlineData("Day")]
    [InlineData("Week")]
    [InlineData("Month")]
    public void TheThreeRecurrenceUnitsAreValid(string unit)
    {
        Assert.Empty(ChoreRules.Check(ValidFields() with { RecurrenceUnit = unit }, requireAll: true));
    }

    [Theory]
    [InlineData("day")]
    [InlineData("Year")]
    [InlineData("")]
    public void AnUnknownRecurrenceUnitIsRejected(string unit)
    {
        Assert.Contains("recurrenceUnit", ChoreRules.Check(ValidFields() with { RecurrenceUnit = unit }, requireAll: true).Keys);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(365, true)]
    [InlineData(0, false)]
    [InlineData(366, false)]
    public void TheRecurrenceIntervalIsBetweenOneAndThreeHundredSixtyFive(int every, bool valid)
    {
        var errors = ChoreRules.Check(ValidFields() with { RecurrenceEvery = every }, requireAll: true);

        Assert.Equal(valid, !errors.ContainsKey("recurrenceEvery"));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void TheWeightIsBetweenOneAndFive(int weight, bool valid)
    {
        var errors = ChoreRules.Check(ValidFields() with { Weight = weight }, requireAll: true);

        Assert.Equal(valid, !errors.ContainsKey("weight"));
    }

    [Fact]
    public void AChoreNeedsAtLeastOneParticipant()
    {
        Assert.Contains("participantIds", ChoreRules.Check(ValidFields() with { ParticipantIds = [] }, requireAll: true).Keys);
    }

    [Fact]
    public void AParticipantCanAppearOnlyOnce()
    {
        var same = Guid.Parse("00000000-0000-0000-0000-00000000000b");

        Assert.Contains("participantIds", ChoreRules.Check(ValidFields() with { ParticipantIds = [same, same] }, requireAll: true).Keys);
    }
}
