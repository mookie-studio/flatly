using Flatly.Api.Data;
using Flatly.Api.Data.Tenancy;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Flatly.Api.Modules.Flats.Scope;

/// <summary>
/// The flat route convention (ADR-002): endpoints that read or change one flat's data sit under <c>/{flatId}</c>.
/// </summary>
public static class FlatRoute
{
    /// <summary>Name of the route parameter that holds the flat id.</summary>
    public const string FlatIdParameter = "flatId";

    /// <summary>
    /// Maps a group under <c>/{flatId}</c>. Every endpoint in it runs only for an active member of that flat: the
    /// <see cref="RequireFlatMembership"/> filter sets <see cref="CurrentFlat"/> and answers 404 for anyone else.
    /// </summary>
    public static RouteGroupBuilder MapFlatScope(this RouteGroupBuilder group)
    {
        var flatGroup = group.MapGroup($"/{{{FlatIdParameter}:guid}}");
        flatGroup.AddEndpointFilter<RequireFlatMembership>();
        return flatGroup;
    }
}

/// <summary>
/// Checks that the signed-in account has an active membership in the flat of the route, and puts that flat in
/// <see cref="CurrentFlat"/>. A missing sign-in answers 401; a flat the caller does not belong to answers 404, so the
/// existence of other flats is not revealed.
/// </summary>
public sealed class RequireFlatMembership(
    CurrentAccountResolver accounts,
    CurrentFlat currentFlat,
    FlatlyDbContext database) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var cancellationToken = http.RequestAborted;

        var account = await accounts.FindAsync(http.User, cancellationToken);
        if (account is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!Guid.TryParse(http.GetRouteValue(FlatRoute.FlatIdParameter)?.ToString(), out var flatId))
        {
            return TypedResults.NotFound();
        }

        // The flat is set before the membership check, so the global filter also applies to that check.
        currentFlat.Id = flatId;
        var isActiveMember = await database.Memberships.AsNoTracking()
            .AnyAsync(
                membership => membership.FlatId == flatId && membership.UserId == account.Id && membership.LeftAt == null,
                cancellationToken);
        if (!isActiveMember)
        {
            return TypedResults.NotFound();
        }

        return await next(context);
    }
}
