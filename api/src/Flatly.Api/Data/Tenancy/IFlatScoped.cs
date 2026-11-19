namespace Flatly.Api.Data.Tenancy;

/// <summary>
/// An entity that belongs to one flat (ADR-002). <see cref="FlatlyDbContext"/> returns such rows only for the flat in
/// <see cref="CurrentFlat"/>, and a module adds this interface to each of its flat-scoped entities.
/// </summary>
public interface IFlatScoped
{
    /// <summary>The flat the row belongs to.</summary>
    Guid FlatId { get; }
}
