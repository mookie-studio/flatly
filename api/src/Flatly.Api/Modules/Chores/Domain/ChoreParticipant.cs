using Flatly.Api.Data.Tenancy;

namespace Flatly.Api.Modules.Chores.Domain;

/// <summary>
/// A member who takes part in the rotation of one chore. Flat-scoped like its chore, so the flat filter also
/// applies to it. Only active members of the flat can be participants; the endpoints check that.
/// </summary>
public sealed class ChoreParticipant : IFlatScoped
{
    /// <summary>The chore the member takes part in.</summary>
    public Guid ChoreId { get; set; }

    /// <summary>The flat of the chore.</summary>
    public Guid FlatId { get; set; }

    /// <summary>The account of the member.</summary>
    public Guid UserId { get; set; }
}
