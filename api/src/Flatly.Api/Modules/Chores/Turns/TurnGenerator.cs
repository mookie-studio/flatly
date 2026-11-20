using Flatly.Api.Data;
using Flatly.Api.Modules.Chores.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Turns;

/// <summary>
/// The turn engine: creates the turns of chores ahead for <see cref="TurnPlanner.HorizonDays"/> days, and replaces the
/// future pending turns of a chore when it changes. Done turns and turns in the past are never touched.
/// </summary>
/// <remarks>
/// Queries here filter by the flat explicitly and ignore the request's flat scope, because the scheduled job runs
/// without a request and must reach every flat. Each public method commits in one transaction.
/// Days are UTC days of the clock, so a turn is due on the UTC day it falls on.
/// </remarks>
public sealed class TurnGenerator(FlatlyDbContext database, TimeProvider clock)
{
    /// <summary>
    /// Replaces the future pending turns of a chore with a fresh plan, and saves the chore's own changes in the same
    /// transaction. A chore that is deactivated only loses its future pending turns. Call it after every change to a chore.
    /// </summary>
    public async Task RegenerateAsync(Chore chore, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chore);

        var today = Today();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        // Deleted before the plan reads the history, so the removed turns no longer count towards the rotation.
        await database.Turns.IgnoreQueryFilters()
            .Where(turn => turn.FlatId == chore.FlatId
                && turn.ChoreId == chore.Id
                && turn.Status == TurnStatus.Pending
                && turn.DueOn >= today)
            .ExecuteDeleteAsync(cancellationToken);

        if (chore.IsActive)
        {
            var history = await LoadHistoryAsync(chore.FlatId, cancellationToken);
            var schedule = new ChoreSchedule(
                chore.Id,
                DateOnly.FromDateTime(chore.CreatedAt.UtcDateTime),
                chore.RecurrenceUnit,
                chore.RecurrenceEvery,
                chore.Weight,
                chore.Participants.Select(participant => participant.UserId).ToList());
            AddPlanned(chore.FlatId, TurnPlanner.Plan([schedule], history, today, today.AddDays(TurnPlanner.HorizonDays)));
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>
    /// Extends the horizon of every active chore of every flat that has one. Occurrences that already have a turn are
    /// skipped, so running it again creates nothing new.
    /// </summary>
    public async Task ExtendAllFlatsAsync(CancellationToken cancellationToken)
    {
        var flatIds = await database.Chores.IgnoreQueryFilters().AsNoTracking()
            .Where(chore => chore.IsActive)
            .Select(chore => chore.FlatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var flatId in flatIds)
        {
            await ExtendFlatAsync(flatId, cancellationToken);
        }
    }

    /// <summary>Extends the horizon of the active chores of one flat, in one transaction.</summary>
    public async Task ExtendFlatAsync(Guid flatId, CancellationToken cancellationToken)
    {
        var today = Today();
        var chores = await database.Chores.IgnoreQueryFilters().AsNoTracking()
            .Include(chore => chore.Participants)
            .Where(chore => chore.FlatId == flatId && chore.IsActive)
            .ToListAsync(cancellationToken);
        if (chores.Count == 0)
        {
            return;
        }

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var history = await LoadHistoryAsync(flatId, cancellationToken);
        var schedules = chores.Select(chore => new ChoreSchedule(
            chore.Id,
            DateOnly.FromDateTime(chore.CreatedAt.UtcDateTime),
            chore.RecurrenceUnit,
            chore.RecurrenceEvery,
            chore.Weight,
            chore.Participants.Select(participant => participant.UserId).ToList()));
        AddPlanned(flatId, TurnPlanner.Plan(schedules, history, today, today.AddDays(TurnPlanner.HorizonDays)));

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private async Task<List<TurnRecord>> LoadHistoryAsync(Guid flatId, CancellationToken cancellationToken) =>
        await database.Turns.IgnoreQueryFilters().AsNoTracking()
            .Where(turn => turn.FlatId == flatId)
            .Select(turn => new TurnRecord(turn.ChoreId, turn.AssigneeId, turn.DueOn, turn.WeightSnapshot))
            .ToListAsync(cancellationToken);

    private void AddPlanned(Guid flatId, IEnumerable<PlannedTurn> planned)
    {
        var now = clock.GetUtcNow();
        foreach (var turn in planned)
        {
            database.Turns.Add(new Turn
            {
                Id = Guid.NewGuid(),
                FlatId = flatId,
                ChoreId = turn.ChoreId,
                AssigneeId = turn.AssigneeId,
                DueOn = turn.DueOn,
                Status = TurnStatus.Pending,
                WeightSnapshot = turn.Weight,
                CreatedAt = now,
                DoneAt = null,
            });
        }
    }
}
