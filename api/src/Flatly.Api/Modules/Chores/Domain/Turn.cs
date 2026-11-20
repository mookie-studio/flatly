using Flatly.Api.Data.Tenancy;

namespace Flatly.Api.Modules.Chores.Domain;

/// <summary>
/// One occurrence of a chore, assigned to a member (ADR-002: flat-scoped). The turn engine creates turns ahead for a
/// fixed horizon. The weight is copied from the chore when the turn is created, so later changes to the chore's weight
/// do not rewrite the history of turns already made. At most one turn exists per chore and due date.
/// </summary>
public sealed class Turn : IFlatScoped
{
    /// <summary>Identifier of the turn.</summary>
    public Guid Id { get; set; }

    /// <summary>The flat the turn belongs to.</summary>
    public Guid FlatId { get; set; }

    /// <summary>The chore the turn is an occurrence of.</summary>
    public Guid ChoreId { get; set; }

    /// <summary>The member who takes the turn.</summary>
    public Guid AssigneeId { get; set; }

    /// <summary>The day the turn is due, in UTC.</summary>
    public DateOnly DueOn { get; set; }

    /// <summary>Whether the turn is still to be done or has been confirmed.</summary>
    public TurnStatus Status { get; set; }

    /// <summary>The weight of the chore when the turn was created.</summary>
    public int WeightSnapshot { get; set; }

    /// <summary>When the turn was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
