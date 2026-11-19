using System.Security.Claims;
using Flatly.Api.Data;
using Flatly.Api.Modules.Flats;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Validation;
using Flatly.Api.Modules.Platform.Identity;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Flatly.Api.Modules.Flats.Endpoints;

/// <summary>Request to create a flat.</summary>
/// <param name="Name">Required, at most 80 characters.</param>
public sealed record CreateFlatRequest(string? Name);

/// <summary>Maps <c>POST /</c>: creates a flat and makes the signed-in account its admin.</summary>
public static class CreateFlatEndpoint
{
    /// <summary>Maps the endpoint under the given group.</summary>
    public static RouteGroupBuilder MapCreateFlat(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateAsync)
            .AddEndpointFilter<ValidationFilter<CreateFlatRequest>>()
            .WithName("CreateFlat")
            .WithSummary("Creates a flat. The signed-in account becomes its admin.");
        return group;
    }

    private static async Task<Results<Created<FlatResponse>, UnauthorizedHttpResult>> CreateAsync(
        CreateFlatRequest request,
        ClaimsPrincipal principal,
        CurrentAccountResolver accounts,
        FlatlyDbContext database,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var account = await accounts.FindAsync(principal, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        var now = clock.GetUtcNow();
        var flat = new Flat { Id = Guid.NewGuid(), Name = request.Name!.Trim(), CreatedAt = now };
        database.Flats.Add(flat);
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flat.Id,
            UserId = account.Id,
            Role = MemberRole.Admin,
            JoinedAt = now,
        });
        // The flat and its admin membership are written by one SaveChanges, so both exist or neither does.
        await database.SaveChangesAsync(cancellationToken);

        var response = new FlatResponse(flat.Id, flat.Name, flat.CreatedAt, MemberRole.Admin.ToString());
        return TypedResults.Created($"{FlatsModule.RoutePrefix}/{flat.Id}", response);
    }
}
