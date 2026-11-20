using System.Net;
using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Chores.Domain;
using Flatly.Api.Modules.Chores.Endpoints;
using Flatly.Api.Modules.Chores.Turns;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Chores;

/// <summary>
/// Creates, edits and extends the turns of chores over HTTP and the scheduled job, against the real PostgreSQL and a
/// clock the test moves. Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ChoreTurnsTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private const string ChoresUrl = "/api/v1/chores";

    [SkippableFact]
    public async Task CreatingAChoreGivesItsTurnsForTheNextFourWeeks()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "turns-create-ana");
        var bruno = await CreateAccountAsync(factory, "turns-create-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Turns create flat");
        await AddMemberAsync(factory, flat.Id, bruno);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}",
            new CreateChoreRequest("Bins", "Week", 1, 2, [anaId, brunoId]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var chore = await response.Content.ReadFromJsonAsync<ChoreResponse>();
        var turns = await TurnsOfChoreAsync(factory, chore!.Id);
        Assert.Equal(
            [Start.Date, Start.Date.AddDays(7), Start.Date.AddDays(14), Start.Date.AddDays(21)],
            turns.Select(turn => turn.DueOn.ToDateTime(TimeOnly.MinValue)).ToArray());
        Assert.All(turns, turn => Assert.Equal(TurnStatus.Pending, turn.Status));
        Assert.All(turns, turn => Assert.Equal(2, turn.WeightSnapshot));
        Assert.Equal(2, turns.Count(turn => turn.AssigneeId == anaId));
        Assert.Equal(2, turns.Count(turn => turn.AssigneeId == brunoId));
    }

    [SkippableFact]
    public async Task EditingAChoreRegeneratesFuturePendingTurnsAndKeepsDoneOnes()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "turns-edit-ana");
        var bruno = await CreateAccountAsync(factory, "turns-edit-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Turns edit flat");
        await AddMemberAsync(factory, flat.Id, bruno);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);
        var created = await PostChoreAsync(anaClient, flat.Id, [anaId, brunoId]);
        var firstTurn = (await TurnsOfChoreAsync(factory, created.Id)).First();
        await MarkDoneAsync(factory, firstTurn.Id);

        var response = await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{created.Id}",
            new UpdateChoreRequest(null, null, null, null, [anaId], null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turns = await TurnsOfChoreAsync(factory, created.Id);
        Assert.Equal(4, turns.Count);
        var done = Assert.Single(turns, turn => turn.Status == TurnStatus.Done);
        Assert.Equal(firstTurn.Id, done.Id);
        Assert.Equal(firstTurn.AssigneeId, done.AssigneeId);
        Assert.All(turns.Where(turn => turn.Status == TurnStatus.Pending), turn => Assert.Equal(anaId, turn.AssigneeId));
    }

    [SkippableFact]
    public async Task TheScheduledJobExtendsTheHorizonWithoutDuplicatingTurns()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "turns-job-ana");
        var bruno = await CreateAccountAsync(factory, "turns-job-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Turns job flat");
        await AddMemberAsync(factory, flat.Id, bruno);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);
        var created = await PostChoreAsync(anaClient, flat.Id, [anaId, brunoId]);
        Assert.Equal(4, (await TurnsOfChoreAsync(factory, created.Id)).Count);

        // Two weeks later the horizon reaches two more weeks. The job runs twice, as it would on two days.
        factory.Clock.Advance(TimeSpan.FromDays(14));
        await RunHorizonJobAsync(factory);
        await RunHorizonJobAsync(factory);

        var turns = await TurnsOfChoreAsync(factory, created.Id);
        Assert.Equal(6, turns.Count);
        Assert.Equal(6, turns.Select(turn => turn.DueOn).Distinct().Count());
        Assert.Contains(turns, turn => turn.DueOn == DateOnly.FromDateTime(Start.AddDays(35).UtcDateTime));
    }

    private static async Task<ChoreResponse> PostChoreAsync(HttpClient client, Guid flatId, Guid[] participants)
    {
        var response = await client.PostAsJsonAsync(
            $"{ChoresUrl}/{flatId}",
            new CreateChoreRequest("Bins", "Week", 1, 2, participants));
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task RunHorizonJobAsync(FlatlyApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TurnHorizonJobs>().ExtendAsync();
    }

    private static async Task<List<Turn>> TurnsOfChoreAsync(FlatlyApiFactory factory, Guid choreId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        return await database.Turns.IgnoreQueryFilters().AsNoTracking()
            .Where(turn => turn.ChoreId == choreId)
            .OrderBy(turn => turn.DueOn)
            .ToListAsync();
    }

    private static async Task MarkDoneAsync(FlatlyApiFactory factory, Guid turnId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        await database.Turns.IgnoreQueryFilters()
            .Where(turn => turn.Id == turnId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(turn => turn.Status, TurnStatus.Done));
    }

    private static async Task<FlatResponse> CreateFlatAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest(name));
        return (await response.Content.ReadFromJsonAsync<FlatResponse>())!;
    }

    private static async Task<FakeExternalAccount> CreateAccountAsync(FlatlyApiFactory factory, string name)
    {
        var account = new FakeExternalAccount(ExternalProviders.Google, $"{name}-{Guid.NewGuid():N}", $"{name}@example.com", name);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        await accounts.FindOrCreateAsync(new ExternalIdentity(account.Provider, account.Subject, account.Email, account.Name));
        return account;
    }

    private static async Task<Guid> UserIdAsync(FlatlyApiFactory factory, FakeExternalAccount account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        return login.UserId;
    }

    private static async Task AddMemberAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            UserId = login.UserId,
            Role = MemberRole.Member,
            JoinedAt = Start,
            LeftAt = null,
        });
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
