namespace Flatly.Api.Modules.Chores.Rotation;

/// <summary>
/// One member who could take the next turn of a chore, with the figures the rotation needs.
/// Plain data: no database or clock is involved.
/// </summary>
/// <param name="MemberId">The member who could take the turn.</param>
/// <param name="AccumulatedWeight">Sum of the weights of the member's turns inside the rolling window.</param>
/// <param name="LastTurnOfChore">When the member last held a turn of this same chore, or <c>null</c> if never.</param>
public readonly record struct RotationCandidate(Guid MemberId, int AccumulatedWeight, DateTimeOffset? LastTurnOfChore);

/// <summary>
/// Weighted rotation: decides who takes the next turn of a chore so that accumulated effort stays balanced.
/// </summary>
public static class WeightedRotation
{
    /// <summary>
    /// Returns the member with the lowest accumulated weight. Ties go to the member who held this chore least
    /// recently, and a member who has never held it comes before one who has. Any remaining tie is broken by
    /// member id, so the same input always gives the same answer.
    /// Returns <c>null</c> when there are no candidates.
    /// </summary>
    /// <param name="candidates">The members taking part in the rotation right now.</param>
    public static Guid? NextAssignee(IEnumerable<RotationCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        return candidates
            .OrderBy(candidate => candidate.AccumulatedWeight)
            .ThenBy(candidate => candidate.LastTurnOfChore.HasValue)
            .ThenBy(candidate => candidate.LastTurnOfChore ?? DateTimeOffset.MinValue)
            .ThenBy(candidate => candidate.MemberId)
            .Select(candidate => (Guid?)candidate.MemberId)
            .FirstOrDefault();
    }
}
