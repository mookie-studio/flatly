namespace Flatly.Api.Modules.Flats.Domain;

/// <summary>
/// A user's place in a flat. Leaving a flat sets <see cref="LeftAt"/> and keeps the row, so the flat's history stays
/// (ADR-002). A user has at most one active membership per flat, but may join again after leaving.
/// </summary>
public sealed class Membership
{
    /// <summary>Identifier of the membership.</summary>
    public Guid Id { get; set; }

    /// <summary>The flat the user belongs to.</summary>
    public Guid FlatId { get; set; }

    /// <summary>The account of the member.</summary>
    public Guid UserId { get; set; }

    /// <summary>What the member may do in the flat.</summary>
    public MemberRole Role { get; set; }

    /// <summary>When the member joined, in UTC.</summary>
    public DateTimeOffset JoinedAt { get; set; }

    /// <summary>When the member left, in UTC. Null while the membership is active.</summary>
    public DateTimeOffset? LeftAt { get; set; }
}
