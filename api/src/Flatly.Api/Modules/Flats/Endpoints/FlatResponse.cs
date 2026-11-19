namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>A flat as seen by one of its members.</summary>
/// <param name="Id">Identifier of the flat.</param>
/// <param name="Name">Name of the flat.</param>
/// <param name="CreatedAt">When the flat was created, in UTC.</param>
/// <param name="Role">The caller's role in the flat: <c>Admin</c> or <c>Member</c>.</param>
public sealed record FlatResponse(Guid Id, string Name, DateTimeOffset CreatedAt, string Role);
