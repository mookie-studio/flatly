using System.Security.Claims;
using System.Threading.RateLimiting;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Flats.Validation;
using Flatly.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Flatly.Api.Modules.Flats;

/// <summary>Registration of the Flats module: flats, members and invitations.</summary>
public static class FlatsModule
{
    /// <summary>Route prefix of every endpoint in this module.</summary>
    public const string RoutePrefix = "/api/v1/flats";

    /// <summary>Registers the services this module needs.</summary>
    public static IServiceCollection AddFlatsModule(this IServiceCollection services)
    {
        services.AddSingleton<IRequestValidator<CreateFlatRequest>, CreateFlatRequestValidator>();
        services.AddSingleton<IRequestValidator<UpdateFlatRequest>, UpdateFlatRequestValidator>();
        services.AddSingleton<IRequestValidator<ChangeMemberRoleRequest>, ChangeMemberRoleRequestValidator>();
        services.AddSingleton<IRequestValidator<JoinFlatRequest>, JoinFlatRequestValidator>();
        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // Join attempts are limited per account (the signed-in name identifier). Every attempt counts, so repeated
            // wrong codes are throttled too. Counters live in this process; each instance keeps its own.
            limiter.AddPolicy(JoinFlatEndpoint.RateLimitPolicy, http => RateLimitPartition.GetFixedWindowLimiter(
                JoinPartitionKey(http),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = JoinAttemptsPerWindow,
                    Window = JoinWindow,
                    QueueLimit = 0,
                }));
        });
        return services;
    }

    /// <summary>Join attempts an account may make in one <see cref="JoinWindow"/>.</summary>
    public const int JoinAttemptsPerWindow = 10;

    /// <summary>Length of the window in which join attempts are counted.</summary>
    public static readonly TimeSpan JoinWindow = TimeSpan.FromMinutes(15);

    private static string JoinPartitionKey(HttpContext http) =>
        http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? http.Connection.RemoteIpAddress?.ToString()
        ?? "anonymous";

    /// <summary>Maps the endpoints of this module under <see cref="RoutePrefix"/>.</summary>
    public static IEndpointRouteBuilder MapFlatsModule(this IEndpointRouteBuilder app)
    {
        app.MapGroup(RoutePrefix)
            .WithTags("Flats")
            .MapCreateFlat()
            .MapListMyFlats()
            .MapGetFlat()
            .MapRenameFlat()
            .MapListMembers()
            .MapLeaveFlat()
            .MapRemoveMember()
            .MapChangeMemberRole()
            .MapInviteCode()
            .MapJoinFlat();
        return app;
    }
}
