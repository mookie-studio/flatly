using System.Net;
using System.Net.Http.Json;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Platform.Endpoints;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Identity;

/// <summary>
/// Checks the session rules over HTTP: default-deny, public endpoints, sign-in recognition, and antiforgery on sign-out.
/// Requests are authenticated with the fake external provider's ticket (see <see cref="FakeExternalSignIn"/>).
/// The clients use https, because the session and antiforgery cookies are Secure.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class SessionTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string SessionUrl = "/api/v1/platform/auth/session";
    private const string SignOutUrl = "/api/v1/platform/auth/sign-out";
    private const string AntiforgeryTokenUrl = "/api/v1/platform/auth/antiforgery-token";
    private const string GoogleSignInUrl = "/api/v1/platform/auth/google/sign-in?returnUrl=/flats";
    private const string MicrosoftSignInUrl = "/api/v1/platform/auth/microsoft/sign-in?returnUrl=/flats";
    private const string ClockUrl = "/api/v1/platform/clock-sample";
    private static readonly Uri SecureBase = new("https://localhost");

    [SkippableFact]
    public async Task AnEndpointWithoutAPublicMarkerReturns401WhenSignedOut()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);

        var response = await client.GetAsync(SessionUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task ExplicitlyPublicEndpointsAnswerWithoutASession()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, allowRedirects: false);

        foreach (var url in new[] { "/health", "/openapi/v1.json", ClockUrl, AntiforgeryTokenUrl, GoogleSignInUrl, MicrosoftSignInUrl })
        {
            var response = await client.GetAsync(url);
            Assert.False(response.StatusCode == HttpStatusCode.Unauthorized, $"{url} requires a session");
        }
    }

    [SkippableFact]
    public async Task AFirstSignInCreatesTheAccountAndTheSessionReturnsIt()
    {
        await using var factory = await CreateFactoryAsync();
        var subject = NewSubject();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, SignIn(factory, subject, "Ana Lopez"));

        var session = await client.GetFromJsonAsync<SessionResponse>(SessionUrl);

        Assert.NotNull(session);
        Assert.Equal("Ana Lopez", session.DisplayName);
        Assert.Equal(session.UserId, (await FindAccountAsync(factory, subject)).Id);
    }

    [SkippableFact]
    public async Task ALaterSignInRecognisesTheSameAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var subject = NewSubject();
        using var first = CreateClient(factory);
        first.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, SignIn(factory, subject, "Ana Lopez"));
        var firstSession = await first.GetFromJsonAsync<SessionResponse>(SessionUrl);

        using var later = CreateClient(factory);
        later.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, SignIn(factory, subject, "Ana L."));
        var laterSession = await later.GetFromJsonAsync<SessionResponse>(SessionUrl);

        Assert.Equal(firstSession!.UserId, laterSession!.UserId);
    }

    [SkippableFact]
    public async Task SignOutWithoutAnAntiforgeryTokenIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        client.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, SignIn(factory, NewSubject(), "Ana"));

        var response = await client.PostAsync(SignOutUrl, null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task SignOutWithTheAntiforgeryTokenSucceeds()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory);
        var token = await client.GetFromJsonAsync<AntiforgeryTokenResponse>(AntiforgeryTokenUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, SignOutUrl);
        request.Headers.Add("X-XSRF-TOKEN", token!.RequestToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [SkippableFact]
    public async Task GoogleSignInRedirectsToGoogle()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, allowRedirects: false);

        var response = await client.GetAsync(GoogleSignInUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://accounts.google.com/", response.Headers.Location!.AbsoluteUri, StringComparison.Ordinal);
    }

    [SkippableFact]
    public async Task MicrosoftSignInRedirectsToMicrosoftForAllAccountTypes()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = CreateClient(factory, allowRedirects: false);

        var response = await client.GetAsync(MicrosoftSignInUrl);

        // The common tenant lets work, school and personal (Outlook, Hotmail, Live) accounts sign in.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://login.microsoftonline.com/common/oauth2/v2.0/authorize", response.Headers.Location!.AbsoluteUri, StringComparison.Ordinal);
    }

    private static string NewSubject() => Guid.NewGuid().ToString("N");

    private static string SignIn(FlatlyApiFactory factory, string subject, string name) =>
        factory.ExternalSignIn.SignIn(new FakeExternalAccount("Google", subject, "ana@example.com", name));

    private static async Task<FlatlyUser> FindAccountAsync(FlatlyApiFactory factory, string subject)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<FlatlyUser>>();
        return await users.FindByLoginAsync(ExternalProviders.Google, subject)
            ?? throw new InvalidOperationException("The account was not created.");
    }

    private static HttpClient CreateClient(FlatlyApiFactory factory, bool allowRedirects = true) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = SecureBase,
            AllowAutoRedirect = allowRedirects,
        });

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
