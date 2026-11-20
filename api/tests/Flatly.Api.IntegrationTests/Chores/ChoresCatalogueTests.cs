#pragma warning disable CA1861 // Test data: the catalogue keys read best inline at each call.
using System.Net;
using System.Net.Http.Json;
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
/// Calls the predefined-chore endpoints over HTTP against the real PostgreSQL (#90). Every test uses its own provider
/// subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ChoresCatalogueTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private const string ChoresUrl = "/api/v1/chores";
    private const string CatalogueUrl = "/api/v1/chores/catalogue";

    [SkippableFact]
    public async Task TheCatalogueAnswersInTheRequestedLanguage()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-lang-ana");
        using var anaClient = factory.CreateSignedInClient(ana);

        var english = await anaClient.GetFromJsonAsync<List<PredefinedChoreResponse>>($"{CatalogueUrl}?lang=en");
        var spanish = await anaClient.GetFromJsonAsync<List<PredefinedChoreResponse>>($"{CatalogueUrl}?lang=es");
        var unsupported = await anaClient.GetAsync($"{CatalogueUrl}?lang=fr");

        Assert.Equal(8, english!.Count);
        Assert.Equal("General cleaning", english[0].Name);
        Assert.Equal("Limpieza general", spanish![0].Name);
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);
    }

    [SkippableFact]
    public async Task WithoutALanguageTheCatalogueFollowsTheAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-default-ana");
        await SetLanguageAsync(factory, ana, UserLanguages.English);
        using var anaClient = factory.CreateSignedInClient(ana);

        var catalogue = await anaClient.GetFromJsonAsync<List<PredefinedChoreResponse>>(CatalogueUrl);

        Assert.Equal("Take out the trash", catalogue!.Single(chore => chore.Key == "trash").Name);
    }

    [SkippableFact]
    public async Task AddingPredefinedChoresCreatesEditableChoresForEveryActiveMember()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-add-ana");
        var bruno = await CreateAccountAsync(factory, "catalogue-add-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Catalogue add flat");
        await AddMemberAsync(factory, flat.Id, bruno);
        var anaId = await UserIdAsync(factory, ana);
        var brunoId = await UserIdAsync(factory, bruno);

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            Add("trash", "cleaning"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var added = await response.Content.ReadFromJsonAsync<AddFromCatalogueResponse>();
        Assert.Empty(added!.Skipped);
        Assert.Equal(new[] { "Limpieza general", "Sacar la basura" }.OrderBy(name => name).ToArray(),
            added.Created.Select(chore => chore.Name).OrderBy(name => name).ToArray());
        var trash = added.Created.Single(chore => chore.Name == "Sacar la basura");
        Assert.Equal("Day", trash.RecurrenceUnit);
        Assert.Equal(2, trash.RecurrenceEvery);
        Assert.Equal(1, trash.Weight);
        Assert.True(trash.IsActive);
        Assert.Equal(new[] { anaId, brunoId }.OrderBy(id => id).ToArray(), trash.ParticipantIds.ToArray());

        // The new chores are normal chores: they can be edited like any other.
        var edited = await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{trash.Id}",
            new UpdateChoreRequest("Sacar el cubo", null, null, Weight: 2, ParticipantIds: null, IsActive: null));
        Assert.Equal("Sacar el cubo", (await edited.Content.ReadFromJsonAsync<ChoreResponse>())!.Name);
    }

    [SkippableFact]
    public async Task AddingTheSamePredefinedChoreTwiceDoesNotDuplicateIt()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-dup-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Catalogue duplicate flat");

        await anaClient.PostAsJsonAsync($"{ChoresUrl}/{flat.Id}/from-catalogue", Add("trash"));
        var second = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            Add("trash", "kitchen"));

        var added = await second.Content.ReadFromJsonAsync<AddFromCatalogueResponse>();
        Assert.Equal(new[] { "trash" }, added!.Skipped.ToArray());
        Assert.Single(added.Created);
        var listed = await anaClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{flat.Id}");
        Assert.Equal(2, listed!.Count);
    }

    [SkippableFact]
    public async Task ADeactivatedPredefinedChoreIsNotAddedAgain()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-deactivated-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Catalogue deactivated flat");
        var added = await (await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            Add("recycling"))).Content.ReadFromJsonAsync<AddFromCatalogueResponse>();
        await anaClient.PatchAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/{added!.Created[0].Id}",
            new UpdateChoreRequest(null, null, null, null, null, IsActive: false));

        var again = await (await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            Add("recycling"))).Content.ReadFromJsonAsync<AddFromCatalogueResponse>();

        Assert.Empty(again!.Created);
        Assert.Equal(new[] { "recycling" }, again.Skipped.ToArray());
    }

    [SkippableFact]
    public async Task ExplicitParticipantsMustBeActiveMembers()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-participant-ana");
        var stranger = await CreateAccountAsync(factory, "catalogue-participant-stranger");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Catalogue participants flat");
        var strangerId = await UserIdAsync(factory, stranger);

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            new AddFromCatalogueRequest(["bathroom"], [strangerId]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task AnUnknownKeyIsRejectedAndNothingIsAdded()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-unknown-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await CreateFlatAsync(anaClient, "Catalogue unknown flat");

        var response = await anaClient.PostAsJsonAsync(
            $"{ChoresUrl}/{flat.Id}/from-catalogue",
            Add("ironing"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var listed = await anaClient.GetFromJsonAsync<List<ChoreResponse>>($"{ChoresUrl}/{flat.Id}?includeInactive=true");
        Assert.Empty(listed!);
    }

    [SkippableFact]
    public async Task ANonMemberGetsNotFoundToAddFromTheCatalogue()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "catalogue-iso-ana");
        var bruno = await CreateAccountAsync(factory, "catalogue-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await CreateFlatAsync(anaClient, "Ana catalogue flat");

        await FlatIsolation.AssertNotFoundForNonMemberAsync(
            brunoClient,
            anasFlat.Id,
            id => $"{ChoresUrl}/{id}/from-catalogue",
            HttpMethod.Post);
    }

    private static AddFromCatalogueRequest Add(params string[] keys) => new(keys, null);

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

    private static async Task SetLanguageAsync(FlatlyApiFactory factory, FakeExternalAccount account, string language)
    {
        var userId = await UserIdAsync(factory, account);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var user = await database.Users.SingleAsync(item => item.Id == userId);
        user.PreferredLanguage = language;
        await database.SaveChangesAsync();
    }

    private static async Task<FlatResponse> CreateFlatAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest(name));
        return (await response.Content.ReadFromJsonAsync<FlatResponse>())!;
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
        });
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
