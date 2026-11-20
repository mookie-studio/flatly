using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Chores.Catalogue;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Validation;
using Flatly.Api.Modules.Flats.Scope;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>
/// Maps <c>POST /{flatId}/from-catalogue</c>: adds predefined chores to a flat as normal, editable chores, named in the
/// caller's language with the catalogue's default recurrence and weight. A key the flat already has, active or not, is
/// skipped, so adding the same predefined chore twice does not duplicate it.
/// </summary>
public static class AddFromCatalogueEndpoint
{
    /// <summary>Maps the endpoint under the given group, inside the flat route (see <c>FlatRoute</c>).</summary>
    public static RouteGroupBuilder MapAddFromCatalogue(this RouteGroupBuilder group)
    {
        group.MapFlatScope().MapPost("/from-catalogue", AddAsync)
            .AddEndpointFilter<ValidationFilter<AddFromCatalogueRequest>>()
            .WithName("AddChoresFromCatalogue")
            .WithSummary("Adds predefined chores to a flat. Keys the flat already has are skipped. Participants default to every active member.");
        return group;
    }

    private static async Task<Results<Ok<AddFromCatalogueResponse>, ValidationProblem, UnauthorizedHttpResult>> AddAsync(
        Guid flatId,
        AddFromCatalogueRequest? request,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        // The body is optional at binding, so a non-member gets 404 from the flat route before this check runs.
        if (request is null)
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]> { ["request"] = ["A request body is required."] });
        }

        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        // The filter has already checked the request, so the keys are known, distinct and present.
        var keys = request.Keys!;
        var existing = await database.Chores.AsNoTracking()
            .Where(chore => chore.CatalogueKey != null && keys.Contains(chore.CatalogueKey))
            .Select(chore => chore.CatalogueKey!)
            .ToListAsync(cancellationToken);
        var toAdd = keys.Where(key => !existing.Contains(key)).ToList();
        var skipped = keys.Where(key => existing.Contains(key)).ToList();

        IReadOnlyList<Guid> participants;
        if (request.ParticipantIds is null)
        {
            // The membership filter limits this to the flat in scope.
            participants = await database.Memberships.AsNoTracking()
                .Where(membership => membership.LeftAt == null)
                .Select(membership => membership.UserId)
                .OrderBy(id => id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            if (!await ChoreMembers.AllAreActiveMembersAsync(database, request.ParticipantIds, cancellationToken))
            {
                return TypedResults.ValidationProblem(ChoreMembers.NotActiveMemberError());
            }

            participants = request.ParticipantIds;
        }

        var now = clock.GetUtcNow();
        var created = new List<Chore>();
        foreach (var key in toAdd)
        {
            var predefined = PredefinedChores.Find(key)!;
            var chore = new Chore
            {
                Id = Guid.NewGuid(),
                FlatId = flatId,
                Name = predefined.NameFor(account.PreferredLanguage),
                RecurrenceUnit = predefined.RecurrenceUnit,
                RecurrenceEvery = predefined.RecurrenceEvery,
                Weight = predefined.Weight,
                IsActive = true,
                CreatedAt = now,
                CatalogueKey = key,
            };
            ChoreMembers.ReplaceParticipants(chore, participants);
            database.Chores.Add(chore);
            created.Add(chore);
        }

        if (created.Count > 0)
        {
            await database.SaveChangesAsync(cancellationToken);
        }

        IReadOnlyList<ChoreResponse> createdResponse = created.Select(ChoreResponse.From).ToList();
        return TypedResults.Ok(new AddFromCatalogueResponse(createdResponse, skipped));
    }
}
