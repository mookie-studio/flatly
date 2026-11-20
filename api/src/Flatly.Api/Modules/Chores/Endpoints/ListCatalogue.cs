using System.Security.Claims;
using Flatly.Api.Modules.Chores.Catalogue;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flatly.Api.Modules.Chores.Endpoints;

/// <summary>
/// Maps <c>GET /catalogue</c>: the predefined chores in the requested language (<c>lang=es</c> or <c>lang=en</c>). Without
/// <c>lang</c> the caller's preferred language is used. The catalogue is the same for every flat, so it is not flat-scoped.
/// </summary>
public static class ListCatalogueEndpoint
{
    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapListCatalogue(this RouteGroupBuilder group)
    {
        group.MapGet("/catalogue", ListAsync)
            .WithName("ListChoreCatalogue")
            .WithSummary("Lists the predefined chores in the requested language (es or en). Without lang, the caller's language is used.");
        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<PredefinedChoreResponse>>, ValidationProblem, UnauthorizedHttpResult>> ListAsync(
        string? lang,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var language = lang ?? account.PreferredLanguage;
        if (language is not (UserLanguages.Spanish or UserLanguages.English))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                ["lang"] = [$"Language must be {UserLanguages.Spanish} or {UserLanguages.English}."],
            });
        }

        IReadOnlyList<PredefinedChoreResponse> response = PredefinedChores.All
            .Select(chore => new PredefinedChoreResponse(
                chore.Key,
                chore.NameFor(language),
                chore.RecurrenceUnit.ToString(),
                chore.RecurrenceEvery,
                chore.Weight))
            .ToList();
        return TypedResults.Ok(response);
    }
}
