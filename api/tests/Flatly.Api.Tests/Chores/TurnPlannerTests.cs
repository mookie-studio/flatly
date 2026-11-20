using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Turns;
using Xunit;

namespace Flatly.Api.Tests.Chores;

public class TurnPlannerTests
{
    private static readonly Guid Ana = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Ben = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid Bathroom = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid Bins = Guid.Parse("00000000-0000-0000-0000-000000000102");

    private static readonly DateOnly Thursday = new(2026, 11, 19);
    private static readonly DateOnly FourWeeksLater = Thursday.AddDays(TurnPlanner.HorizonDays);

    [Fact]
    public void WeeklyOccurrencesCountFromTheAnchorInsideTheRange()
    {
        var dates = TurnPlanner.Occurrences(Thursday, RecurrenceUnit.Week, 1, Thursday, FourWeeksLater);

        Assert.Equal(
            [new DateOnly(2026, 11, 19), new DateOnly(2026, 11, 26), new DateOnly(2026, 12, 3), new DateOnly(2026, 12, 10)],
            dates);
    }

    [Fact]
    public void MonthlyOccurrencesKeepTheAnchorDayAcrossMonthEnds()
    {
        var anchor = new DateOnly(2026, 1, 31);

        var dates = TurnPlanner.Occurrences(anchor, RecurrenceUnit.Month, 1, new DateOnly(2026, 2, 1), new DateOnly(2026, 5, 1));

        // February has no 31st, so it falls on its last day; March comes back to the 31st, not to the 28th.
        Assert.Equal([new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30)], dates);
    }

    [Fact]
    public void PlanningGivesEachOccurrenceToTheMemberWhoHasHadLeast()
    {
        var chore = new ChoreSchedule(Bathroom, Thursday, RecurrenceUnit.Week, 1, 2, [Ben, Ana]);

        var planned = TurnPlanner.Plan([chore], [], Thursday, FourWeeksLater);

        Assert.Equal(4, planned.Count);
        Assert.Equal([Ana, Ben, Ana, Ben], planned.Select(turn => turn.AssigneeId).ToArray());
    }

    [Fact]
    public void PlanningTwiceCreatesNothingTheSecondTime()
    {
        var chore = new ChoreSchedule(Bathroom, Thursday, RecurrenceUnit.Week, 1, 2, [Ana, Ben]);
        var first = TurnPlanner.Plan([chore], [], Thursday, FourWeeksLater);
        var existing = first.Select(turn => new TurnRecord(turn.ChoreId, turn.AssigneeId, turn.DueOn, turn.Weight)).ToList();

        var second = TurnPlanner.Plan([chore], existing, Thursday, FourWeeksLater);

        Assert.Empty(second);
    }

    [Fact]
    public void DoneTurnsCountTowardsTheRotation()
    {
        // Ana did a heavy chore yesterday, so the light chore goes to Ben even though Ana was listed first.
        var heavyYesterday = new TurnRecord(Bins, Ana, Thursday.AddDays(-1), 5);
        var chore = new ChoreSchedule(Bathroom, Thursday, RecurrenceUnit.Day, 7, 1, [Ana, Ben]);

        var planned = TurnPlanner.Plan([chore], [heavyYesterday], Thursday, Thursday.AddDays(1));

        Assert.Equal(Ben, Assert.Single(planned).AssigneeId);
    }

    [Fact]
    public void OccurrencesAreBalancedAcrossChoresOfTheFlat()
    {
        var sameDay = Thursday;
        var bathroom = new ChoreSchedule(Bathroom, sameDay, RecurrenceUnit.Week, 1, 5, [Ana, Ben]);
        var bins = new ChoreSchedule(Bins, sameDay, RecurrenceUnit.Week, 1, 1, [Ana, Ben]);

        var planned = TurnPlanner.Plan([bins, bathroom], [], sameDay, sameDay.AddDays(1));

        // Both chores fall on the same day. Ties are handed out by chore id, so the bathroom (lower id) goes first to
        // Ana by member id, and the bins then go to Ben, who has no weight yet. The two turns never share a member.
        Assert.Equal(2, planned.Count);
        Assert.NotEqual(planned[0].AssigneeId, planned[1].AssigneeId);
    }

    [Fact]
    public void AChoreWithNoParticipantsPlansNothing()
    {
        var chore = new ChoreSchedule(Bathroom, Thursday, RecurrenceUnit.Week, 1, 2, []);

        Assert.Empty(TurnPlanner.Plan([chore], [], Thursday, FourWeeksLater));
    }
}
