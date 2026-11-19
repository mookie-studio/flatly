using System.Net;
using System.Net.Http.Json;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Platform;

/// <summary>
/// Calls the profile endpoints over HTTP, signed in through the fake provider, and checks what they store.
/// Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class ProfileTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string ProfileUrl = "/api/v1/platform/profile";

    [SkippableFact]
    public async Task GetReturnsTheDefaultsOfAFirstSignIn()
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana Lopez");
        using var client = Client(factory, account);

        var profile = await client.GetFromJsonAsync<ProfileResponse>(ProfileUrl);

        Assert.Equal("Ana Lopez", profile!.DisplayName);
        Assert.Equal("es", profile.PreferredLanguage);
        Assert.Equal("Europe/Madrid", profile.TimeZoneId);
        Assert.Null(profile.AvatarUrl);
    }

    [SkippableFact]
    public async Task GetWithoutSignInIsUnauthorized()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ProfileUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetForAnAccountThatWasNeverCreatedIsUnauthorized()
    {
        await using var factory = await CreateFactoryAsync();
        var ghost = new FakeExternalAccount(ExternalProviders.Google, NewSubject(), "ghost@example.com");
        using var client = Client(factory, ghost);

        var response = await client.GetAsync(ProfileUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task PutReplacesEveryFieldAndGetReturnsThem()
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);
        var update = new UpdateProfileRequest("  Ana L.  ", "https://cdn.example.com/ana.png", "en", "America/Mexico_City");

        var put = await client.PutAsJsonAsync(ProfileUrl, update);
        var profile = await client.GetFromJsonAsync<ProfileResponse>(ProfileUrl);

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal(new ProfileResponse("Ana L.", "https://cdn.example.com/ana.png", "en", "America/Mexico_City"), profile);
    }

    [SkippableFact]
    public async Task PutWithoutAnAvatarRemovesIt()
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);
        await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana", "https://cdn.example.com/ana.png", "es", "Europe/Madrid"));

        var put = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana", null, "es", "Europe/Madrid"));

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var profile = await client.GetFromJsonAsync<ProfileResponse>(ProfileUrl);
        Assert.Null(profile!.AvatarUrl);
    }

    [SkippableFact]
    public async Task PutChangesOnlyTheCallersAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await SignInAsync(factory, "Ana");
        var bruno = await SignInAsync(factory, "Bruno");

        await Client(factory, ana).PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana New", null, "en", "Europe/London"));

        var brunoProfile = await Client(factory, bruno).GetFromJsonAsync<ProfileResponse>(ProfileUrl);
        Assert.Equal("Bruno", brunoProfile!.DisplayName);
        Assert.Equal("es", brunoProfile.PreferredLanguage);
    }

    [SkippableTheory]
    [InlineData("Europe/Atlantis")]
    [InlineData("Madrid")]
    [InlineData("")]
    public async Task AnUnknownTimeZoneIsRejected(string timeZoneId)
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);

        var response = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana", null, "es", timeZoneId));

        await AssertValidationErrorAsync(response, "timeZoneId");
    }

    [SkippableTheory]
    [InlineData("fr")]
    [InlineData("ES")]
    [InlineData("")]
    public async Task OnlySpanishAndEnglishAreAcceptedAsLanguage(string language)
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);

        var response = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana", null, language, "Europe/Madrid"));

        await AssertValidationErrorAsync(response, "preferredLanguage");
    }

    [SkippableTheory]
    [InlineData("http://insecure.example.com/ana.png")]
    [InlineData("ana.png")]
    [InlineData("javascript:alert(1)")]
    public async Task AnAvatarThatIsNotAnHttpsUrlIsRejected(string avatarUrl)
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);

        var response = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Ana", avatarUrl, "es", "Europe/Madrid"));

        await AssertValidationErrorAsync(response, "avatarUrl");
    }

    [SkippableFact]
    public async Task ANameThatIsBlankOrTooLongIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        var account = await SignInAsync(factory, "Ana");
        using var client = Client(factory, account);

        var blank = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("   ", null, "es", "Europe/Madrid"));
        var tooLong = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest(new string('a', 81), null, "es", "Europe/Madrid"));

        await AssertValidationErrorAsync(blank, "displayName");
        await AssertValidationErrorAsync(tooLong, "displayName");
    }

    [SkippableFact]
    public async Task PutWithoutSignInIsUnauthorizedAndChangesNothing()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync(ProfileUrl, new UpdateProfileRequest("Mallory", null, "en", "Europe/London"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task AssertValidationErrorAsync(HttpResponseMessage response, string field)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>();
        Assert.True(problem!.Errors.ContainsKey(field), $"Expected a validation error for '{field}'.");
    }

    private static HttpClient Client(FlatlyApiFactory factory, FakeExternalAccount account) => factory.CreateSignedInClient(account);

    /// <summary>Creates the account behind a fake provider sign-in and returns the fake account that signs in as it.</summary>
    private static async Task<FakeExternalAccount> SignInAsync(FlatlyApiFactory factory, string name)
    {
        var account = new FakeExternalAccount(ExternalProviders.Google, NewSubject(), $"{name.ToLowerInvariant()}@example.com", name);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        await accounts.FindOrCreateAsync(new ExternalIdentity(account.Provider, account.Subject, account.Email, account.Name));
        return account;
    }

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }

    /// <summary>The part of a ProblemDetails body that the validation tests read.</summary>
    private sealed record ValidationProblemBody(Dictionary<string, string[]> Errors);
}
