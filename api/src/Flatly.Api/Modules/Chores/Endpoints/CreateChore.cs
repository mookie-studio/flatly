using Flatly.Api.Data;
using Flatly.Api.Modules.Chores;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>Maps <c>POST /{flatId}</c>: creates a chore. Every member of the flat may create one.</summary>
public static class CreateChoreEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <see cref="FlatRoute"/>).</summary>
    public static RouteGroupBuilder MapCreateChore(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateChoreRequest>>()
            .WithName("CreateChore")
            .WithSummary("Creates a chore in a flat. Its participants must be active members of the flat (422 otherwise).");
        return group;
    }

    private static async Task<Results<Created<ChoreResponse>, ValidationProblem>> CreateAsync(
        Guid flatId,
        CreateChoreRequest? request,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The body is optional at binding, so a non-member gets 404 from the flat route before this check runs.
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A request body is required."] });
        }

        // The filter has already checked the request, so every field is present and valid.
        var participantIds = request.ParticipantIds!;
        if (!await ChoreMembers.AllAreActiveMembersAsync(database, participantIds, cancellationToken))
        {
            return TypedResults.ValidationProblem(ChoreMembers.NotActiveMemberError());
        }

        var chore = new Chore
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            Name = request.Name!.Trim(),
            RecurrenceUnit = Enum.Parse<RecurrenceUnit>(request.RecurrenceUnit!),
            RecurrenceEvery = request.RecurrenceEvery!.Value,
            Weight = request.Weight!.Value,
            IsActive = true,
            CreatedAt = clock.GetUtcNow(),
        };
        ChoreMembers.ReplaceParticipants(chore, participantIds);
        database.Chores.Add(chore);
        await database.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"{ChoresModule.RoutePrefix}/{flatId}/{chore.Id}", ChoreResponse.From(chore));
    }
}
