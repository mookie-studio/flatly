using System.Net;
using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Flats;

/// <summary>
/// Calls the flat endpoints over HTTP, as signed-in accounts, against the real PostgreSQL.
/// Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class FlatsEndpointsTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private static readonly string[] AnaFlatNames = ["Ana flat one", "Ana flat two"];
    private static readonly string[] BrunoFlatNames = ["Bruno flat"];

    [SkippableFact]
    public async Task CreatingAFlatMakesTheCreatorItsAdmin()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana");
        using var client = factory.CreateSignedInClient(ana);

        var response = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Calle Mayor 4"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var flat = await response.Content.ReadFromJsonAsync<FlatResponse>();
        Assert.Equal("Calle Mayor 4", flat!.Name);
        Assert.Equal("Admin", flat.Role);
        Assert.Equal(Start, flat.CreatedAt);
        Assert.Equal($"{FlatsUrl}/{flat.Id}", response.Headers.Location?.OriginalString);

        var membership = await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject);
        Assert.Equal(MemberRole.Admin, membership.Role);
        Assert.Null(membership.LeftAt);
    }

    [SkippableFact]
    public async Task CreatingAFlatTrimsTheName()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateSignedInClient(await CreateAccountAsync(factory, "trim"));

        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("  Piso Sur  "))).Content
            .ReadFromJsonAsync<FlatResponse>();

        Assert.Equal("Piso Sur", flat!.Name);
    }

    [SkippableFact]
    public async Task AFlatNameThatIsEmptyOrTooLongIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateSignedInClient(await CreateAccountAsync(factory, "invalid"));

        var empty = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("   "));
        var tooLong = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest(new string('x', 81)));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Contains("name", (await empty.Content.ReadAsStringAsync()));
    }

    [SkippableFact]
    public async Task ListShowsOnlyTheFlatsTheCallerBelongsTo()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "list-ana");
        var bruno = await CreateAccountAsync(factory, "list-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Ana flat one"));
        await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Ana flat two"));
        await brunoClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Bruno flat"));

        var anaFlats = await anaClient.GetFromJsonAsync<List<FlatResponse>>(FlatsUrl);
        var brunoFlats = await brunoClient.GetFromJsonAsync<List<FlatResponse>>(FlatsUrl);

        Assert.Equal(AnaFlatNames, anaFlats!.Select(flat => flat.Name).Order());
        Assert.Equal(BrunoFlatNames, brunoFlats!.Select(flat => flat.Name).Order());
    }

    [SkippableFact]
    public async Task ListLeavesOutAFlatTheCallerHasLeft()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "left-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Old flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await LeaveFlatAsync(factory, flat!.Id, ana);

        var flats = await client.GetFromJsonAsync<List<FlatResponse>>(FlatsUrl);

        Assert.DoesNotContain(flats!, item => item.Id == flat.Id);
    }

    [SkippableFact]
    public async Task AMemberGetsTheFlat()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateSignedInClient(await CreateAccountAsync(factory, "get-member"));
        var created = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Mine"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var response = await client.GetAsync($"{FlatsUrl}/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mine", (await response.Content.ReadFromJsonAsync<FlatResponse>())!.Name);
    }

    [SkippableFact]
    public async Task AFlatOfAnotherMemberAnswersNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "isolation-ana");
        var bruno = await CreateAccountAsync(factory, "isolation-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Ana only"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var response = await brunoClient.GetAsync($"{FlatsUrl}/{anasFlat!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task AFormerMemberCannotGetTheFlatAnyMore()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "former-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Left behind"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await LeaveFlatAsync(factory, flat!.Id, ana);

        var response = await client.GetAsync($"{FlatsUrl}/{flat.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task RequestsWithoutASignInAreRejected()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();

        var list = await client.GetAsync(FlatsUrl);
        var create = await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Anonymous"));

        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
    }

    private static async Task<FakeExternalAccount> CreateAccountAsync(FlatlyApiFactory factory, string name)
    {
        var account = new FakeExternalAccount(ExternalProviders.Google, $"{name}-{Guid.NewGuid():N}", $"{name}@example.com", name);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        await accounts.FindOrCreateAsync(new ExternalIdentity(account.Provider, account.Subject, account.Email, account.Name));
        return account;
    }

    private static async Task<Membership> FindMembershipAsync(FlatlyApiFactory factory, Guid flatId, string provider, string subject)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == provider && item.ProviderKey == subject);
        return await database.Memberships.SingleAsync(membership => membership.FlatId == flatId && membership.UserId == login.UserId);
    }

    private static async Task LeaveFlatAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        var membership = await database.Memberships.SingleAsync(item => item.FlatId == flatId && item.UserId == login.UserId);
        membership.LeftAt = Start;
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
