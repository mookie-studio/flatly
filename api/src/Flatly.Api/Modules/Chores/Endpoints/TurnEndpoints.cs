using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Data.Tenancy;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>
/// Maps the turn endpoints under <c>/{flatId}/turns</c>: listing a period, and marking a turn done or undoing that.
/// Only the assignee confirms or undoes a turn. Nothing here reassigns a turn, so an overdue turn stays with its assignee.
/// </summary>
public static class TurnEndpoints
{
    /// <summary>Longest period a list request may ask for, in days.</summary>
    public const int MaxPeriodDays = 120;

    /// <summary>How many days before today the default period starts, so recent overdue turns are listed.</summary>
    public const int DefaultLookBackDays = 7;

    /// <summary>How many days after today the default period ends, inclusive: the rotation horizon.</summary>
    public const int DefaultLookAheadDays = 27;

    /// <summary>Maps the turn endpoints under the given chores group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapTurns(this RouteGroupBuilder group)
    {
        var flat = group.MapFlatScope();
        flat.MapGet("/turns", ListAsync)
            .WithName("ListTurns")
            .WithSummary(
                "Lists the turns of a flat for a period (from and to, inclusive; default from 7 days ago to 27 days ahead). " +
                "Set mine=true for the signed-in member's turns only. Overdue turns carry isOverdue.");
        flat.MapPost("/turns/{turnId:guid}/done", MarkDoneAsync)
            .WithName("MarkTurnDone")
            .WithSummary("Marks a turn done. Only its assignee may do so (403 otherwise). Marking a done turn again changes nothing.");
        flat.MapPost("/turns/{turnId:guid}/undo", UndoAsync)
            .WithName("UndoTurn")
            .WithSummary("Undoes a done turn, so it is pending again. Only its assignee may do so (403 otherwise).");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<TurnResponse>>, UnauthorizedHttpResult, ValidationProblem>> ListAsync(
        Guid flatId,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken,
        DateOnly? from = null,
        DateOnly? to = null,
        bool mine = false)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var start = from ?? today.AddDays(-DefaultLookBackDays);
        var end = to ?? today.AddDays(DefaultLookAheadDays);
        if (end < start || end.DayNumber - start.DayNumber > MaxPeriodDays)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["to"] = [$"The period must end on or after its start and be at most {MaxPeriodDays} days long."],
            });
        }

        // The flat route has already checked the membership, and the query filters limit the rows to the flat in scope.
        var turns = database.Turns.AsNoTracking()
            .Where(turn => turn.DueOn >= start && turn.DueOn <= end)
            .Where(turn => !mine || turn.AssigneeId == account.Id);
        var rows = await turns
            .Join(
                database.Chores.AsNoTracking(),
                turn => turn.ChoreId,
                chore => chore.Id,
                (turn, chore) => new { turn, chore.Name })
            .OrderBy(row => row.turn.DueOn)
            .ThenBy(row => row.Name)
            .ThenBy(row => row.turn.Id)
            .ToListAsync(cancellationToken);

        IReadOnlyList<TurnResponse> response = rows
            .Select(row => ToResponse(row.turn, row.Name, today))
            .ToList();
        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TurnResponse>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> MarkDoneAsync(
        Guid flatId,
        Guid turnId,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var found = await FindTurnAsync(flatId, turnId, database, cancellationToken);
        if (found is null)
        {
            return TypedResults.NotFound();
        }

        var (turn, choreName) = found.Value;
        if (turn.AssigneeId != account.Id)
        {
            return TypedResults.Forbid();
        }

        if (turn.Status == TurnStatus.Pending)
        {
            turn.Status = TurnStatus.Done;
            turn.DoneAt = clock.GetUtcNow();
            await database.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToResponse(turn, choreName, Today(clock)));
    }

    private static async Task<Results<Ok<TurnResponse>, NotFound, UnauthorizedHttpResult, ForbidHttpResult>> UndoAsync(
        Guid flatId,
        Guid turnId,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var found = await FindTurnAsync(flatId, turnId, database, cancellationToken);
        if (found is null)
        {
            return TypedResults.NotFound();
        }

        var (turn, choreName) = found.Value;
        if (turn.AssigneeId != account.Id)
        {
            return TypedResults.Forbid();
        }

        if (turn.Status == TurnStatus.Done)
        {
            turn.Status = TurnStatus.Pending;
            turn.DoneAt = null;
            await database.SaveChangesAsync(cancellationToken);
        }

        return TypedResults.Ok(ToResponse(turn, choreName, Today(clock)));
    }

    // Loads the turn tracked, so done and undo can change it. The query filter limits the lookup to the flat in scope.
    private static async Task<(Turn Turn, string ChoreName)?> FindTurnAsync(
        Guid flatId,
        Guid turnId,
        FlatlyDbContext database,
        CancellationToken cancellationToken)
    {
        var row = await database.Turns
            .Where(turn => turn.Id == turnId && turn.FlatId == flatId)
            .Join(database.Chores, turn => turn.ChoreId, chore => chore.Id, (turn, chore) => new { turn, chore.Name })
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (row.turn, row.Name);
    }

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

    private static TurnResponse ToResponse(Turn turn, string choreName, DateOnly today) => new(
        turn.Id,
        turn.ChoreId,
        choreName,
        turn.AssigneeId,
        turn.DueOn,
        turn.Status.ToString(),
        turn.WeightSnapshot,
        turn.DoneAt,
        turn.Status == TurnStatus.Pending && turn.DueOn < today);
}
