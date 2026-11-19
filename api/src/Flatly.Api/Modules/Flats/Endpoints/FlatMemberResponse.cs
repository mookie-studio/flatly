using Flatly.Api.Modules.Flats.Domain;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>An active member of a flat, as seen by the other members.</summary>
/// <param name="UserId">Identifier of the member's account.</param>
/// <param name="DisplayName">Name the member shows in the flat.</param>
/// <param name="AvatarUrl">Absolute https URL of the avatar, or null when there is none.</param>
/// <param name="Role">The member's role in the flat: <c>Admin</c> or <c>Member</c>.</param>
/// <param name="JoinedAt">When the member joined the flat, in UTC.</param>
public sealed record FlatMemberResponse(Guid UserId, string DisplayName, string? AvatarUrl, string Role, DateTimeOffset JoinedAt);
