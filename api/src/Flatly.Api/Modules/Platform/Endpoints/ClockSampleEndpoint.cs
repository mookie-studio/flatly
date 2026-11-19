using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Flatly.Api.Modules.Platform.Endpoints;

/// <summary>Response of the clock sample: the current instant as the application's clock reports it.</summary>
/// <param name="UtcNow">The current time, in UTC.</param>
public sealed record ClockSampleResponse(DateTimeOffset UtcNow);

/// <summary>
/// Sample endpoint for the injectable clock. It reads the time from <see cref="TimeProvider"/>, so integration tests
/// can set and advance it. Remove it once the first real endpoint reads time the same way.
/// </summary>
public static class ClockSampleEndpoint
{
    /// <summary>Maps <c>GET clock-sample</c> under the given group.</summary>
    public static RouteGroupBuilder MapClockSample(this RouteGroupBuilder group)
    {
        group.MapGet("/clock-sample", (TimeProvider clock) => TypedResults.Ok(new ClockSampleResponse(clock.GetUtcNow())))
            .WithName("ClockSample")
            .WithSummary("Sample endpoint returning the time from the injectable clock.");
        return group;
    }
}
