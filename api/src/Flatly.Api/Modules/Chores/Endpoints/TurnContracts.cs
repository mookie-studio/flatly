namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>A turn as the API returns it.</summary>
/// <param name="Id">Identifier of the turn.</param>
/// <param name="ChoreId">The chore the turn is an occurrence of.</param>
/// <param name="ChoreName">The name of that chore.</param>
/// <param name="AssigneeId">The member who has the turn.</param>
/// <param name="DueOn">The day the turn is due, in UTC.</param>
/// <param name="Status">"Pending" or "Done".</param>
/// <param name="Weight">The weight of the turn, copied from its chore when it was created.</param>
/// <param name="DoneAt">When the turn was confirmed, or null while it is pending.</param>
/// <param name="IsOverdue">True while the turn is pending and its due day is before today. An overdue turn keeps its assignee.</param>
public sealed record TurnResponse(
    Guid Id,
    Guid ChoreId,
    string ChoreName,
    Guid AssigneeId,
    DateOnly DueOn,
    string Status,
    int Weight,
    DateTimeOffset? DoneAt,
    bool IsOverdue);
