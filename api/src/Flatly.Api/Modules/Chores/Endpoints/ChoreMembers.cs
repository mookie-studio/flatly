using Flatly.Api.Data;
using Flatly.Api.Modules.Chores.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>Checks and replaces the participants of a chore. Participants must be active members of the flat.</summary>
internal static class ChoreMembers
{
    /// <summary>The error returned when a participant is not an active member of the flat.</summary>
    public static Dictionary<string, string[]> NotActiveMemberError() => new()
    {
        ["participantIds"] = ["Every participant must be an active member of the flat."],
    };

    /// <summary>True when every account is an active member of the flat in scope (see <c>FlatRoute</c>).</summary>
    public static async Task<bool> AllAreActiveMembersAsync(
        FlatlyDbContext database,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var wanted = userIds.Distinct().ToList();
        // The membership filter limits this count to the flat in scope.
        var activeCount = await database.Memberships.AsNoTracking()
            .CountAsync(membership => membership.LeftAt == null && wanted.Contains(membership.UserId), cancellationToken);
        return activeCount == wanted.Count;
    }

    /// <summary>Makes the chore's participants exactly <paramref name="userIds"/>, keeping the rows that stay.</summary>
    public static void ReplaceParticipants(Chore chore, IReadOnlyList<Guid> userIds)
    {
        var wanted = userIds.ToHashSet();
        foreach (var removed in chore.Participants.Where(participant => !wanted.Contains(participant.UserId)).ToList())
        {
            chore.Participants.Remove(removed);
        }

        var current = chore.Participants.Select(participant => participant.UserId).ToHashSet();
        foreach (var userId in userIds.Where(id => !current.Contains(id)))
        {
            chore.Participants.Add(new ChoreParticipant
            {
                ChoreId = chore.Id,
                FlatId = chore.FlatId,
                UserId = userId,
            });
        }
    }
}
