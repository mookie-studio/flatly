namespace Flatly.Api.Modules.Flats.Domain;

/// <summary>What a member may do in a flat. The creator of a flat is its <see cref="Admin"/>.</summary>
public enum MemberRole
{
    /// <summary>Manages the flat and its members.</summary>
    Admin,

    /// <summary>Uses the flat's data.</summary>
    Member,
}
