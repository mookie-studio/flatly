using Flatly.Api.Data;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Turns;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>
/// Maps <c>PATCH /{flatId}/{choreId}</c>: changes a chore, or deactivates it with <c>isActive: false</c>. The future
/// pending turns of the chore are regenerated; done turns and past turns are kept.
/// </summary>
public static class UpdateChoreEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapUpdateChore(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPatch("/{choreId:guid}", UpdateAsync)
            .AddEndpointFilter<ValidationFilter<UpdateChoreRequest>>()
            .WithName("UpdateChore")
            .WithSummary("Changes or deactivates a chore of a flat. Only the fields sent are changed. A chore of another flat answers 404.");
        return group;
    }

    private static async Task<Results<Ok<ChoreResponse>, NotFound, ValidationProblem>> UpdateAsync(
        Guid choreId,
        UpdateChoreRequest? request,
        FlatlyDbContext database,
        TurnGenerator turns,
        CancellationToken cancellationToken)
    {
        // The body is optional at binding, so a non-member gets 404 from the flat route before this check runs.
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A request body is required."] });
        }

        // The chores filter limits the lookup to the flat in scope, so a chore of another flat is not found.
        var chore = await database.Chores
            .Include(item => item.Participants)
            .SingleOrDefaultAsync(item => item.Id == choreId, cancellationToken);
        if (chore is null)
        {
            return TypedResults.NotFound();
        }

        if (request.ParticipantIds is not null
            && !await ChoreMembers.AllAreActiveMembersAsync(database, request.ParticipantIds, cancellationToken))
        {
            return TypedResults.ValidationProblem(ChoreMembers.NotActiveMemberError());
        }

        // The filter has already checked the request, so each sent field is valid.
        if (request.Name is not null)
        {
            chore.Name = request.Name.Trim();
        }

        if (request.RecurrenceUnit is not null)
        {
            chore.RecurrenceUnit = Enum.Parse<RecurrenceUnit>(request.RecurrenceUnit);
        }

        if (request.RecurrenceEvery is not null)
        {
            chore.RecurrenceEvery = request.RecurrenceEvery.Value;
        }

        if (request.Weight is not null)
        {
            chore.Weight = request.Weight.Value;
        }

        if (request.IsActive is not null)
        {
            chore.IsActive = request.IsActive.Value;
        }

        if (request.ParticipantIds is not null)
        {
            ChoreMembers.ReplaceParticipants(chore, request.ParticipantIds);
        }

        await turns.RegenerateAsync(chore, cancellationToken);
        return TypedResults.Ok(ChoreResponse.From(chore));
    }
}
