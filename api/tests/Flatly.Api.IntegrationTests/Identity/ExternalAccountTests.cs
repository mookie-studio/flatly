using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Identity;

/// <summary>
/// Checks how external sign-ins create and find accounts, against the real PostgreSQL with the Identity tables.
/// Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ExternalAccountTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    [SkippableFact]
    public async Task FirstSignInCreatesAnAccountPrefilledFromTheProvider()
    {
        await using var factory = await CreateFactoryAsync();
        var subject = NewSubject();

        var user = await SignInAsync(factory, new ExternalIdentity(
            ExternalProviders.Google, subject, "ana@example.com", "Ana Lopez", "https://lh3.example.com/ana.png"));

        Assert.Equal("Ana Lopez", user.DisplayName);
        Assert.Equal("ana@example.com", user.Email);
        Assert.Equal("https://lh3.example.com/ana.png", user.AvatarUrl);
        Assert.Equal(UserLanguages.Spanish, user.PreferredLanguage);
        Assert.Equal(UserLanguages.DefaultTimeZoneId, user.TimeZoneId);
        Assert.Equal(Start, user.CreatedAt);
    }

    [SkippableFact]
    public async Task SecondSignInWithTheSameSubjectReturnsTheSameAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var subject = NewSubject();
        var first = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, subject, "ana@example.com", "Ana"));

        // The provider now reports a new email and name. The account is still the same one.
        var second = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, subject, "ana.new@example.com", "Ana L."));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal("Ana", second.DisplayName);
    }

    [SkippableFact]
    public async Task TheSameEmailFromAnotherProviderCreatesASeparateAccount()
    {
        await using var factory = await CreateFactoryAsync();
        const string email = "shared@example.com";
        var google = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, NewSubject(), email, "Google name"));

        var microsoft = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Microsoft, NewSubject(), email, "Microsoft name"));

        Assert.NotEqual(google.Id, microsoft.Id);
    }

    [SkippableFact]
    public async Task TheSameSubjectFromAnotherProviderIsADifferentAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var subject = NewSubject();
        var google = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, subject));

        var microsoft = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Microsoft, subject));

        Assert.NotEqual(google.Id, microsoft.Id);
    }

    [SkippableFact]
    public async Task AccountsHaveNoLocalPasswordAndNoEmailConfirmation()
    {
        await using var factory = await CreateFactoryAsync();
        var user = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, NewSubject(), "ana@example.com"));

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FlatlyUser>>();
        var stored = await users.FindByIdAsync(user.Id.ToString());

        Assert.False(await users.HasPasswordAsync(stored!));
        Assert.False(stored!.EmailConfirmed);
    }

    [SkippableFact]
    public async Task ConcurrentFirstSignInsOfTheSameAccountCreateOneAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var identity = new ExternalIdentity(ExternalProviders.Google, NewSubject(), "ana@example.com", "Ana");

        var users = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SignInAsync(factory, identity)));

        Assert.Single(users.Select(user => user.Id).Distinct());
    }

    [SkippableFact]
    public async Task MissingDisplayNameIsTakenFromTheEmailAddress()
    {
        await using var factory = await CreateFactoryAsync();

        var user = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Microsoft, NewSubject(), "bruno@outlook.com"));

        Assert.Equal("bruno", user.DisplayName);
    }

    [SkippableFact]
    public async Task MissingNameAndEmailGetsAPlaceholderName()
    {
        await using var factory = await CreateFactoryAsync();

        var user = await SignInAsync(factory, new ExternalIdentity(ExternalProviders.Google, NewSubject()));

        Assert.Equal("Member", user.DisplayName);
        Assert.Null(user.Email);
    }

    [SkippableFact]
    public async Task AvatarUrlsThatAreNotHttpsAreDropped()
    {
        await using var factory = await CreateFactoryAsync();

        var user = await SignInAsync(factory, new ExternalIdentity(
            ExternalProviders.Google, NewSubject(), "ana@example.com", "Ana", "http://insecure.example.com/a.png"));

        Assert.Null(user.AvatarUrl);
    }

    [SkippableFact]
    public async Task AProviderOutsideMvp1IsRejected()
    {
        await using var factory = await CreateFactoryAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            SignInAsync(factory, new ExternalIdentity("Apple", NewSubject())));
    }

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    private static async Task<FlatlyUser> SignInAsync(FlatlyApiFactory factory, ExternalIdentity identity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        return await accounts.FindOrCreateAsync(identity);
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
