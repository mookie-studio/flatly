namespace Flatly.Api.Modules.Chores.Domain;

/// <summary>
/// Where a turn stands. Stored as text, so the database reads the same names as the API.
/// </summary>
public enum TurnStatus
{
    /// <summary>The turn is still to be done. Only pending turns are regenerated when a chore changes.</summary>
    Pending,

    /// <summary>The assignee confirmed the turn. Done turns are history and are never changed by the engine.</summary>
    Done,
}
