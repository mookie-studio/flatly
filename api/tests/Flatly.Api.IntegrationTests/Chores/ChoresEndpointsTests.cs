using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Chores.Endpoints;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Chores;

/// <summary>
/// Calls the chore endpoints over HTTP, as signed-in accounts, against the real PostgreSQL.
/// Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ChoresEndpointsTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private const string ChoresUrl = "/api/v1/chores";

    [SkippableFact]
    public async Task AMemberCreatesAChoreAndListsItWithItsParticipants()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-create-ana");
        var bruno = await CreateAccountAsync(factory, "chore-create-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores create flat");
        await AddMemberAsync(factory, flat.Id, bruno, MemberRole.Member);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}",
            new CreateChoreRequest("  Bañera  ", "Week", 2, 4, [anaId, brunoId]));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<ChoreResponse>();
        Assert.Equal("Bañera", created!.Name);
        Assert.Equal("Week", created.RecurrenceUnit);
        Assert.Equal(2, created.RecurrenceEvery);
        Assert.Equal(4, created.Weight);
        Assert.True(created.IsActive);
        Assert.Equal(Start, created.CreatedAt);
        Assert.Equal(Sorted(anaId, brunoId), created.ParticipantIds.ToArray());
        Assert.Equal($"{ChoresUrl}/{flat.Id}/{created.Id}", response.Headers.Location?.OriginalString);

        var listed = await anaClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{flat.Id}");
        Assert.Equal(new[] { created.Id }, listed!.Select(chore => chore.Id).ToArray());
    }

    [SkippableFact]
    public async Task ACreateWithInvalidFieldsAnswersValidationErrors()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-invalid-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores invalid flat");
        var anaId = await UserIdAsync(factory, ana);

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}",
            new CreateChoreRequest("Basura", "Year", 1, 9, [anaId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await ErrorFieldsAsync(response);
        Assert.Contains("recurrenceUnit", errors);
        Assert.Contains("weight", errors);
    }

    [SkippableFact]
    public async Task ParticipantsMustBeActiveMembersOfTheFlat()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-participant-ana");
        var leftBruno = await CreateAccountAsync(factory, "chore-participant-left");
        var stranger = await CreateAccountAsync(factory, "chore-participant-stranger");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores participants flat");
        await AddMemberAsync(factory, flat.Id, leftBruno, MemberRole.Member, leftAt: Start);
        var anaId = await UserIdAsync(factory, ana);
        var leftId = await UserIdAsync(factory, leftBruno);
        var strangerId = await UserIdAsync(factory, stranger);

        var withLeftMember = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}",
            new CreateChoreRequest("Cocina", "Week", 1, 2, [anaId, leftId]));
        var withStranger = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}",
            new CreateChoreRequest("Cocina", "Week", 1, 2, [anaId, strangerId]));

        Assert.Equal(HttpStatusCode.BadRequest, withLeftMember.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, withStranger.StatusCode);
        Assert.Contains("participantIds", await ErrorFieldsAsync(withLeftMember));
        Assert.Contains("participantIds", await ErrorFieldsAsync(withStranger));
    }

    [SkippableFact]
    public async Task TheListHidesDeactivatedChoresUntilAskedFor()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-list-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores list flat");
        var anaId = await UserIdAsync(factory, ana);
        var kept = await CreateChoreAsync(anaClient, flat.Id, "Basura", anaId);
        var retired = await CreateChoreAsync(anaClient, flat.Id, "Ventanas", anaId);

        var deactivated = await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{retired.Id}",
            new UpdateChoreRequest(null, null, null, null, null, IsActive: false));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        Assert.False((await deactivated.Content.ReadFromJsonAsync<ChoreResponse>())!.IsActive);

        var active = await anaClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{flat.Id}");
        var everything = await anaClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{flat.Id}?includeInactive=true");

        Assert.Equal(new[] { kept.Id }, active!.Select(chore => chore.Id).ToArray());
        Assert.Equal(new[] { kept.Id, retired.Id }.OrderBy(id => id.ToString()).ToArray(),
            everything!.Select(chore => chore.Id).OrderBy(id => id.ToString()).ToArray());
    }

    [SkippableFact]
    public async Task AnUpdateChangesOnlyTheFieldsSentAndCanReplaceParticipants()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-update-ana");
        var bruno = await CreateAccountAsync(factory, "chore-update-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores update flat");
        await AddMemberAsync(factory, flat.Id, bruno, MemberRole.Member);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);
        var chore = await CreateChoreAsync(anaClient, flat.Id, "Baño", anaId, brunoId);

        var response = await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{chore.Id}",
            new UpdateChoreRequest("Baño a fondo", null, null, Weight: 5, ParticipantIds: [brunoId], IsActive: null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ChoreResponse>();
        Assert.Equal("Baño a fondo", updated!.Name);
        Assert.Equal(5, updated.Weight);
        Assert.Equal(chore.RecurrenceUnit, updated.RecurrenceUnit);
        Assert.Equal(chore.RecurrenceEvery, updated.RecurrenceEvery);
        Assert.True(updated.IsActive);
        Assert.Equal(new[] { brunoId }, updated.ParticipantIds.ToArray());
    }

    [SkippableFact]
    public async Task AChoreCanBeDeactivatedAndActivatedAgain()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-toggle-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores toggle flat");
        var anaId = await UserIdAsync(factory, ana);
        var chore = await CreateChoreAsync(anaClient, flat.Id, "Plantas", anaId);

        await anaClient.PatchAsJsonAsync($"{ChoresUrl}/{flat.Id}/{chore.Id}", new UpdateChoreRequest(null, null, null, null, null, false));
        var reactivated = await anaClient.PatchAsJsonAsync($"{ChoresUrl}/{flat.Id}/{chore.Id}", new UpdateChoreRequest(null, null, null, null, null, true));

        Assert.True((await reactivated.Content.ReadFromJsonAsync<ChoreResponse>())!.IsActive);
    }

    [SkippableFact]
    public async Task AnUpdateWithAnInvalidWeightIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-update-invalid-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Chores update invalid flat");
        var anaId = await UserIdAsync(factory, ana);
        var chore = await CreateChoreAsync(anaClient, flat.Id, "Polvo", anaId);

        var response = await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{chore.Id}",
            new UpdateChoreRequest(null, null, null, Weight: 0, ParticipantIds: null, IsActive: null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("weight", await ErrorFieldsAsync(response));
    }

    [SkippableFact]
    public async Task ACoreOfAnotherFlatCannotBeReadOrChangedThroughAnotherFlat()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-iso-ana");
        var bruno = await CreateAccountAsync(factory, "chore-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await CreateFlatAsync(anaClient, "Ana chores flat");
        var brunosFlat = await CreateFlatAsync(brunoClient, "Bruno chores flat");
        var anaChore = await CreateChoreAsync(anaClient, anasFlat.Id, "Ana's chore", await UserIdAsync(factory, ana));

        var patchThroughOwnFlat = await brunoClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{brunosFlat.Id}/{anaChore.Id}",
            new UpdateChoreRequest("Hijacked", null, null, null, null, null));
        var listOfOwnFlat = await brunoClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{brunosFlat.Id}");

        Assert.Equal(HttpStatusCode.NotFound, patchThroughOwnFlat.StatusCode);
        Assert.Empty(listOfOwnFlat!);
    }

    [SkippableFact]
    public async Task ANonMemberGetsNotFoundForEveryChoreEndpoint()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "chore-nonmember-ana");
        var bruno = await CreateAccountAsync(factory, "chore-nonmember-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await CreateFlatAsync(anaClient, "Ana nonmember flat");
        var chore = await CreateChoreAsync(anaClient, anasFlat.Id, "Filtro", await UserIdAsync(factory, ana));

        await FlatIsolation.AssertNotFoundForNonMemberAsync(brunoClient, anasFlat.Id, id => $"{ChoresUrl}/{id}");
        await FlatIsolation.AssertNotFoundForNonMemberAsync(brunoClient, anasFlat.Id, id => $"{ChoresUrl}/{id}", HttpMethod.Post);
        await FlatIsolation.AssertNotFoundForNonMemberAsync(
            brunoClient,
            anasFlat.Id,
            id => $"{ChoresUrl}/{id}/{chore.Id}",
            HttpMethod.Patch);
    }

    private static async Task<ChoreResponse> CreateChoreAsync(HttpClient client, Guid flatId, string name, params Guid[] participants)
    {
        var response = await client.PostAsJsonAsync(
            $"{ChoresUrl}/{flatId}",
            new CreateChoreRequest(name, "Week", 1, 2, participants));
        return (await response.Content.ReadFromJsonAsync<ChoreResponse>())!;
    }

    private static async Task<FlatResponse> CreateFlatAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest(name));
        return (await response.Content.ReadFromJsonAsync<FlatResponse>())!;
    }

    private static async Task<HashSet<string>> ErrorFieldsAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errors").EnumerateObject().Select(property => property.Name).ToHashSet();
    }

    private static Guid[] Sorted(params Guid[] ids) => ids.OrderBy(id => id).ToArray();

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

    private static async Task AddMemberAsync(
        FlatlyApiFactory factory,
        Guid flatId,
        FakeExternalAccount account,
        MemberRole role,
        DateTimeOffset? leftAt = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            UserId = login.UserId,
            Role = role,
            JoinedAt = Start,
            LeftAt = leftAt,
        });
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
