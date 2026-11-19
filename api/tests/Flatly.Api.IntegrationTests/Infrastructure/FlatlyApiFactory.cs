using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.Modules.Platform.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the real API in-process, on the given PostgreSQL, with a clock the test controls.
/// The Development environment applies the migrations when the host starts, as it does locally.
/// Email and external sign-in are replaced with fakes: <see cref="Email"/> holds the outbox and
/// <see cref="ExternalSignIn"/> signs test accounts in.
/// </summary>
public sealed class FlatlyApiFactory(string connectionString, DateTimeOffset now) : WebApplicationFactory<Program>
{
    /// <summary>The clock the API reads time from. Set or advance it in a test to move the application's time.</summary>
    public FakeTimeProvider Clock { get; } = new(now);

    /// <summary>The email sender the API uses. Its outbox lists every message the API sent.</summary>
    public FakeEmailSender Email { get; } = new();

    /// <summary>The fake Google and Microsoft provider. Use it to sign a test account in.</summary>
    public FakeExternalSignIn ExternalSignIn { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting($"ConnectionStrings:{DatabaseRegistration.ConnectionName}", connectionString);
        // Google is configured, so the sign-in endpoint can redirect to it. The provider itself is never called in tests.
        builder.UseSetting("Authentication:Google:ClientId", "test-google-client");
        builder.UseSetting("Authentication:Google:ClientSecret", "test-google-secret");
        builder.UseSetting("Authentication:Microsoft:ClientId", "test-microsoft-client");
        builder.UseSetting("Authentication:Microsoft:ClientSecret", "test-microsoft-secret");
        builder.UseSetting("Web:Origin", "https://web.test");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Email);

            services.AddSingleton(ExternalSignIn);
            // The fake provider is the default scheme of the test host, so requests with a ticket are signed in.
            services.AddAuthentication(options => options.DefaultAuthenticateScheme = FakeExternalAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, FakeExternalAuthenticationHandler>(
                    FakeExternalAuthenticationHandler.SchemeName,
                    _ => { });
            // Requests are authenticated by the fake ticket instead of the session cookie, so tests need no Google login.
            // Everything else (the fallback policy, AllowAnonymous, antiforgery) is the production configuration.
            services.PostConfigure<AuthenticationOptions>(options =>
                options.DefaultScheme = FakeExternalAuthenticationHandler.SchemeName);
        });
    }

    /// <summary>
    /// Creates a client that sends requests as <paramref name="account"/>. The account must already exist in the database:
    /// use <c>ExternalAccountService.FindOrCreateAsync</c> to create it.
    /// </summary>
    public HttpClient CreateSignedInClient(FakeExternalAccount account)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(FakeExternalAuthenticationHandler.TicketHeader, ExternalSignIn.SignIn(account));
        return client;
    }
}
