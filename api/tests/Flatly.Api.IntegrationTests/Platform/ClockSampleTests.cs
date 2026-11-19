using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Platform.Endpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Platform;

/// <summary>
/// Calls the API over HTTP and checks the database it wrote to. Also shows that the test clock is the one the API reads.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ClockSampleTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string ClockUrl = "/api/v1/platform/clock-sample";

    [SkippableFact]
    public async Task ReturnsTheTimeSetOnTheFakeClock()
    {
        await using var factory = await CreateFactoryAsync(Start);
        using var client = factory.CreateClient();

        var body = await client.GetFromJsonAsync<ClockSampleResponse>(ClockUrl);

        Assert.Equal(Start, body!.UtcNow);
    }

    [SkippableFact]
    public async Task AdvancingTheFakeClockMovesTheResponse()
    {
        await using var factory = await CreateFactoryAsync(Start);
        using var client = factory.CreateClient();

        factory.Clock.Advance(TimeSpan.FromDays(3));
        var body = await client.GetFromJsonAsync<ClockSampleResponse>(ClockUrl);

        Assert.Equal(Start.AddDays(3), body!.UtcNow);
    }

    [SkippableFact]
    public async Task MigrationsAreAppliedToRealPostgreSql()
    {
        await using var factory = await CreateFactoryAsync(Start);
        using var _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", database.Database.ProviderName);
        var applied = await database.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, migration => migration.EndsWith("_Initial", StringComparison.Ordinal));
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync(DateTimeOffset now)
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), now);
    }
}
