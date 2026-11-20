using Flatly.Api.Modules.Chores.Domain;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>Request to create a chore. Every field is required.</summary>
/// <param name="Name">The chore's name, at most 80 characters after trimming.</param>
/// <param name="RecurrenceUnit"><c>Day</c>, <c>Week</c> or <c>Month</c>.</param>
/// <param name="RecurrenceEvery">How many units pass between two turns, from 1 to 365.</param>
/// <param name="Weight">How much effort the chore counts for, from 1 to 5.</param>
/// <param name="ParticipantIds">The accounts that take part in the rotation: at least one, each an active member of the flat.</param>
public sealed record CreateChoreRequest(
    string? Name,
    string? RecurrenceUnit,
    int? RecurrenceEvery,
    int? Weight,
    IReadOnlyList<Guid>? ParticipantIds);

/// <summary>Request to change a chore. Send only the fields to change; <c>participantIds</c> replaces the whole set.</summary>
/// <param name="Name">The new name, as for create.</param>
/// <param name="RecurrenceUnit">The new unit, as for create.</param>
/// <param name="RecurrenceEvery">The new interval, as for create.</param>
/// <param name="Weight">The new weight, as for create.</param>
/// <param name="ParticipantIds">The new participants, as for create.</param>
/// <param name="IsActive">False deactivates the chore; true activates it again.</param>
public sealed record UpdateChoreRequest(
    string? Name,
    string? RecurrenceUnit,
    int? RecurrenceEvery,
    int? Weight,
    IReadOnlyList<Guid>? ParticipantIds,
    bool? IsActive);

/// <summary>A chore as the API returns it.</summary>
/// <param name="Id">Identifier of the chore.</param>
/// <param name="Name">The chore's name.</param>
/// <param name="RecurrenceUnit"><c>Day</c>, <c>Week</c> or <c>Month</c>.</param>
/// <param name="RecurrenceEvery">How many units pass between two turns.</param>
/// <param name="Weight">How much effort the chore counts for.</param>
/// <param name="IsActive">Whether the chore takes part in the rotation.</param>
/// <param name="ParticipantIds">The accounts that take part in the rotation.</param>
/// <param name="CreatedAt">When the chore was created, in UTC.</param>
public sealed record ChoreResponse(
    Guid Id,
    string Name,
    string RecurrenceUnit,
    int RecurrenceEvery,
    int Weight,
    bool IsActive,
    IReadOnlyList<Guid> ParticipantIds,
    DateTimeOffset CreatedAt)
{
    /// <summary>Maps a chore with its participants loaded.</summary>
    public static ChoreResponse From(Chore chore) => new(
        chore.Id,
        chore.Name,
        chore.RecurrenceUnit.ToString(),
        chore.RecurrenceEvery,
        chore.Weight,
        chore.IsActive,
        chore.Participants.Select(participant => participant.UserId).OrderBy(id => id).ToList(),
        chore.CreatedAt);
}
