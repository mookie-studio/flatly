using Flatly.Api.Data.Tenancy;

namespace Flatly.Api.Modules.Chores.Domain;

/// <summary>
/// A household task that repeats (ADR-002: flat-scoped). It has a weight, so that some chores count more than others,
/// and the members who take part in its rotation. A deactivated chore keeps its row and stops taking part in the
/// rotation, so the flat's history stays intact.
/// </summary>
public sealed class Chore : IFlatScoped
{
    /// <summary>Identifier of the chore.</summary>
    public Guid Id { get; set; }

    /// <summary>The flat the chore belongs to.</summary>
    public Guid FlatId { get; set; }

    /// <summary>Name shown to the members, trimmed, at most 80 characters.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>The unit of the recurrence, together with <see cref="RecurrenceEvery"/>.</summary>
    public RecurrenceUnit RecurrenceUnit { get; set; }

    /// <summary>How many <see cref="RecurrenceUnit"/> pass between two turns, from 1 to 365.</summary>
    public int RecurrenceEvery { get; set; }

    /// <summary>How much effort the chore counts for, from 1 (light) to 5 (heavy).</summary>
    public int Weight { get; set; }

    /// <summary>False once the chore is deactivated. Only active chores take part in the rotation.</summary>
    public bool IsActive { get; set; }

    /// <summary>When the chore was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The members who take part in the rotation of this chore.</summary>
    public List<ChoreParticipant> Participants { get; set; } = [];
}
