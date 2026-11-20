using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Rotation;

namespace Flatly.Api.Modules.Chores.Turns;

/// <summary>The chore figures the planner needs. Plain data: no database or clock is involved.</summary>
/// <param name="ChoreId">The chore to plan turns for.</param>
/// <param name="Anchor">The first day of the recurrence, the chore's creation day. Occurrences count from it.</param>
/// <param name="Unit">The unit of the recurrence.</param>
/// <param name="Every">How many units pass between two occurrences.</param>
/// <param name="Weight">The weight each turn of this chore adds to its assignee.</param>
/// <param name="Participants">The members taking part in the rotation.</param>
public sealed record ChoreSchedule(
    Guid ChoreId,
    DateOnly Anchor,
    RecurrenceUnit Unit,
    int Every,
    int Weight,
    IReadOnlyList<Guid> Participants);

/// <summary>A turn that already exists, pending or done. It counts towards the rotation and blocks a duplicate.</summary>
/// <param name="ChoreId">The chore of the turn.</param>
/// <param name="AssigneeId">The member who has the turn.</param>
/// <param name="DueOn">The due day of the turn.</param>
/// <param name="Weight">The weight the turn adds to its assignee.</param>
public sealed record TurnRecord(Guid ChoreId, Guid AssigneeId, DateOnly DueOn, int Weight);

/// <summary>A turn the planner decided to create.</summary>
public sealed record PlannedTurn(Guid ChoreId, DateOnly DueOn, Guid AssigneeId, int Weight);

/// <summary>
/// Plans the turns of a flat: which occurrences are missing in a date range, and who takes each one. Pure logic, so the
/// rules can be checked without a database.
/// </summary>
public static class TurnPlanner
{
    /// <summary>How many days ahead turns are created.</summary>
    public const int HorizonDays = 28;

    /// <summary>How many days back the rolling weight of a member counts.</summary>
    public const int WindowDays = 28;

    /// <summary>
    /// The occurrence days of a recurrence that fall in <c>[from, until)</c>. Occurrence <c>k</c> is the anchor plus
    /// <c>k * every</c> units, counted from the anchor (not from the previous occurrence), so month ends do not drift.
    /// </summary>
    public static IReadOnlyList<DateOnly> Occurrences(DateOnly anchor, RecurrenceUnit unit, int every, DateOnly from, DateOnly until)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(every, 1);

        var dates = new List<DateOnly>();
        for (var step = 0; ; step++)
        {
            var date = unit switch
            {
                RecurrenceUnit.Day => anchor.AddDays(step * every),
                RecurrenceUnit.Week => anchor.AddDays(step * every * 7),
                RecurrenceUnit.Month => anchor.AddMonths(step * every),
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown recurrence unit."),
            };

            if (date >= until)
            {
                return dates;
            }

            if (date >= from)
            {
                dates.Add(date);
            }
        }
    }

    /// <summary>
    /// Plans the turns missing in <c>[from, until)</c> for the given chores. An occurrence that already has a turn
    /// (in <paramref name="existing"/>) is skipped, so planning twice gives nothing the second time.
    /// Occurrences are handed out in date order, so the rotation balances across all chores of the flat: each
    /// assignee's weight is the sum of their turns in the rolling window, including the turns planned earlier in this run.
    /// </summary>
    /// <param name="chores">The active chores to plan for.</param>
    /// <param name="existing">Every turn of the flat that already exists, pending or done.</param>
    /// <param name="from">First day to plan, inclusive.</param>
    /// <param name="until">Day after the last day to plan, exclusive.</param>
    public static IReadOnlyList<PlannedTurn> Plan(
        IEnumerable<ChoreSchedule> chores,
        IEnumerable<TurnRecord> existing,
        DateOnly from,
        DateOnly until)
    {
        ArgumentNullException.ThrowIfNull(chores);
        ArgumentNullException.ThrowIfNull(existing);

        var windowStart = from.AddDays(-WindowDays);
        var weights = new Dictionary<Guid, int>();
        var lastTurnOfChore = new Dictionary<(Guid ChoreId, Guid MemberId), DateOnly>();
        var taken = new HashSet<(Guid ChoreId, DateOnly DueOn)>();

        foreach (var turn in existing)
        {
            taken.Add((turn.ChoreId, turn.DueOn));
            if (turn.DueOn >= windowStart)
            {
                weights[turn.AssigneeId] = weights.GetValueOrDefault(turn.AssigneeId) + turn.Weight;
            }

            var key = (turn.ChoreId, turn.AssigneeId);
            if (!lastTurnOfChore.TryGetValue(key, out var last) || turn.DueOn > last)
            {
                lastTurnOfChore[key] = turn.DueOn;
            }
        }

        var due = chores
            .SelectMany(chore => Occurrences(chore.Anchor, chore.Unit, chore.Every, from, until)
                .Where(date => !taken.Contains((chore.ChoreId, date)))
                .Select(date => (DueOn: date, Chore: chore)))
            .OrderBy(item => item.DueOn)
            .ThenBy(item => item.Chore.ChoreId)
            .ToList();

        var planned = new List<PlannedTurn>();
        foreach (var (dueOn, chore) in due)
        {
            var candidates = chore.Participants
                .Distinct()
                .Select(memberId => new RotationCandidate(
                    memberId,
                    weights.GetValueOrDefault(memberId),
                    lastTurnOfChore.TryGetValue((chore.ChoreId, memberId), out var last)
                        ? new DateTimeOffset(last.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                        : null));

            var assignee = WeightedRotation.NextAssignee(candidates);
            if (assignee is not { } memberId)
            {
                continue;
            }

            planned.Add(new PlannedTurn(chore.ChoreId, dueOn, memberId, chore.Weight));
            weights[memberId] = weights.GetValueOrDefault(memberId) + chore.Weight;
            lastTurnOfChore[(chore.ChoreId, memberId)] = dueOn;
        }

        return planned;
    }
}
