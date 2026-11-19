namespace Flatly.Api.Modules.Flats.Domain;

/// <summary>
/// A flat: the tenant of all Flatly data (ADR-002). Members reach the flat's data only through an active <see cref="Membership"/>.
/// </summary>
public sealed class Flat
{
    /// <summary>Identifier of the flat.</summary>
    public Guid Id { get; set; }

    /// <summary>Name the members give the flat, for example "Calle Mayor 4, 2º B".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>When the flat was created, in UTC.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
