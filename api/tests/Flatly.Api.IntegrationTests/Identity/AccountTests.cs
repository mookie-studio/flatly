using System.Net;
using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Identity;

/// <summary>
/// Checks account export and deletion over HTTP against the real PostgreSQL: the export bundle, the last-admin rule,
/// the antiforgery check, and what is left of a deleted account. Every test uses its own provider subjects, because all
/// tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class AccountTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private const string ExportUrl = "/api/v1/platform/account/export";
    private const string AccountUrl = "/api/v1/platform/account";
    private const string SessionUrl = "/api/v1/platform/auth/session";
    private const string AntiforgeryTokenUrl = "/api/v1/platform/auth/antiforgery-token";
    private static readonly Uri SecureBase = new("https://localhost");

    [SkippableFact]
    public async Task TheExportHoldsTheProfileAndTheMembership()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana");
        using var client = CreateSignedInClient(factory, ana);
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Calle Mayor 4"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var export = await client.GetFromJsonAsync<AccountExportResponse>(ExportUrl);

        Assert.NotNull(export);
        Assert.Equal("ana", export.Profile.DisplayName);
        Assert.Equal("ana@example.com", export.Profile.Email);
        var membership = Assert.Single(export.Memberships);
        Assert.Equal(flat!.Id, membership.FlatId);
        Assert.Equal("Calle Mayor 4", membership.FlatName);
        Assert.Equal("Admin", membership.Role);
        Assert.Null(membership.LeftAt);
    }

    [SkippableFact]
    public async Task DeletingAnAccountLeavesItsFlatsKeepsTheHistoryAndScrubsIt()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana-delete");
        var bruno = await CreateAccountAsync(factory, "bruno-delete");
        using var client = CreateSignedInClient(factory, ana);
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Piso Norte"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        var flatId = flat!.Id;
        // Another admin keeps the flat administered after Ana leaves.
        await AddMemberAsync(factory, flatId, bruno, MemberRole.Admin);
        var anaId = (await FindAccountAsync(factory, ana)).Id;

        var response = await PostWithAntiforgeryAsync(client, HttpMethod.Delete, AccountUrl);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FlatlyUser>>();
        var deleted = await users.FindByIdAsync(anaId.ToString("D"));
        Assert.NotNull(deleted);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal(AccountEndpoints.FormerMemberName, deleted.DisplayName);
        Assert.Null(deleted.Email);
        Assert.Null(deleted.AvatarUrl);
        Assert.False(await database.UserLogins.AnyAsync(login => login.UserId == anaId));
        // The membership stays as history, with the leave date set. Bruno is still an active member.
        var anaMembership = await database.Memberships.IgnoreQueryFilters()
            .SingleAsync(item => item.FlatId == flatId && item.UserId == anaId);
        Assert.NotNull(anaMembership.LeftAt);
        var brunoId = (await FindAccountAsync(factory, bruno)).Id;
        var brunoMembership = await database.Memberships.IgnoreQueryFilters()
            .SingleAsync(item => item.FlatId == flatId && item.UserId == brunoId);
        Assert.Null(brunoMembership.LeftAt);
    }

    [SkippableFact]
    public async Task SigningInAgainAfterDeletionCreatesANewAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana-again");
        using var client = CreateSignedInClient(factory, ana);
        var anaId = (await FindAccountAsync(factory, ana)).Id;
        var deletion = await PostWithAntiforgeryAsync(client, HttpMethod.Delete, AccountUrl);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);

        using var later = CreateSignedInClient(factory, ana);
        var session = await later.GetFromJsonAsync<SessionResponse>(SessionUrl);

        Assert.NotNull(session);
        Assert.NotEqual(anaId, session.UserId);
    }

    [SkippableFact]
    public async Task TheLastAdminOfAFlatWithOtherMembersCannotDelete()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana-last");
        var bruno = await CreateAccountAsync(factory, "bruno-last");
        using var client = CreateSignedInClient(factory, ana);
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Piso Sur"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        var flatId = flat!.Id;
        await AddMemberAsync(factory, flatId, bruno, MemberRole.Member);

        var response = await PostWithAntiforgeryAsync(client, HttpMethod.Delete, AccountUrl);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stillThere = await FindAccountAsync(factory, ana);
        Assert.Null(stillThere.DeletedAt);
        var membership = await FindMembershipAsync(factory, flatId, ana);
        Assert.Null(membership.LeftAt);
    }

    [SkippableFact]
    public async Task DeletingWithoutTheAntiforgeryTokenIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "ana-token");
        using var client = CreateSignedInClient(factory, ana);

        var response = await client.DeleteAsync(AccountUrl);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null((await FindAccountAsync(factory, ana)).DeletedAt);
    }

    private static HttpClient CreateSignedInClient(FlatlyApiFactory factory, FakeExternalAccount account)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = SecureBase });
        client.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, factory.ExternalSignIn.SignIn(account));
        return client;
    }

    private static async Task<HttpResponseMessage> PostWithAntiforgeryAsync(HttpClient client, HttpMethod method, string url)
    {
        var token = await client.GetFromJsonAsync<AntiforgeryTokenResponse>(AntiforgeryTokenUrl);
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Add("X-XSRF-TOKEN", token!.RequestToken);
        return await client.SendAsync(request);
    }

    private static async Task<FakeExternalAccount> CreateAccountAsync(FlatlyApiFactory factory, string name)
    {
        var account = new FakeExternalAccount(ExternalProviders.Google, $"{name}-{Guid.NewGuid():N}", $"{name}@example.com", name);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        await accounts.FindOrCreateAsync(new ExternalIdentity(account.Provider, account.Subject, account.Email, account.Name));
        return account;
    }

    private static async Task<FlatlyUser> FindAccountAsync(FlatlyApiFactory factory, FakeExternalAccount account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FlatlyUser>>();
        return await users.FindByLoginAsync(account.Provider, account.Subject)
            ?? throw new InvalidOperationException("The account was not found.");
    }

    private static async Task AddMemberAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account, MemberRole role)
    {
        var user = await FindAccountAsync(factory, account);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            UserId = user.Id,
            Role = role,
            JoinedAt = Start,
        });
        await database.SaveChangesAsync();
    }

    private static async Task<Membership> FindMembershipAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        var user = await FindAccountAsync(factory, account);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        return await database.Memberships.IgnoreQueryFilters()
            .SingleAsync(membership => membership.FlatId == flatId && membership.UserId == user.Id);
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
