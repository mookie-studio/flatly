using Flatly.Api.Data;
using Flatly.Api.Modules.Flats.Scope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>Maps <c>GET /{flatId}</c>: the chores of a flat, by name. Deactivated chores need <c>includeInactive=true</c>.</summary>
public static class ListChoresEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapListChores(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapGet("/", ListAsync)
            .WithName("ListChores")
            .WithSummary("Lists the chores of a flat, by name. Only active members of the flat may read them; others get 404.");
        return group;
    }

    private static async Task<Ok<IReadOnlyList<ChoreResponse>>> ListAsync(
        FlatlyDbContext database,
        CancellationToken cancellationToken,
        bool includeInactive = false)
    {
        // The flat route has already checked the membership, and the query filters limit the rows to the flat in scope.
        var chores = await database.Chores.AsNoTracking()
            .Where(chore => includeInactive || chore.IsActive)
            .Include(chore => chore.Participants)
            .OrderBy(chore => chore.Name)
            .ThenBy(chore => chore.Id)
            .ToListAsync(cancellationToken);

        IReadOnlyList<ChoreResponse> response = chores.Select(ChoreResponse.From).ToList();
        return TypedResults.Ok(response);
    }
}
