using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Flatly.Api.IntegrationTests.Fakes;

/// <summary>An account that the fake external provider (Google or Microsoft) knows about.</summary>
/// <param name="Provider">Provider name, for example <c>Google</c> or <c>Microsoft</c>.</param>
/// <param name="Subject">The provider's stable user id for the account.</param>
/// <param name="Email">Email address the provider reports for the account.</param>
/// <param name="Name">Display name the provider reports, if any.</param>
public sealed record FakeExternalAccount(string Provider, string Subject, string Email, string? Name = null);

/// <summary>
/// Stands in for Google and Microsoft in the integration tests, so tests can sign in as a given account without
/// reaching the real providers. <see cref="SignIn"/> returns a ticket for the account. A request that sends the ticket
/// in the <see cref="FakeExternalAuthenticationHandler.TicketHeader"/> header is authenticated as that account.
/// </summary>
public sealed class FakeExternalSignIn
{
    private readonly ConcurrentDictionary<string, FakeExternalAccount> _tickets = new(StringComparer.Ordinal);

    /// <summary>Signs the account in with the fake provider and returns the ticket that identifies the session.</summary>
    public string SignIn(FakeExternalAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        var ticket = Guid.NewGuid().ToString("N");
        _tickets[ticket] = account;
        return ticket;
    }

    internal bool TryGetAccount(string ticket, out FakeExternalAccount? account) =>
        _tickets.TryGetValue(ticket, out account);
}

/// <summary>
/// Authentication scheme that reads the ticket from <see cref="TicketHeader"/> and turns it into a signed-in principal.
/// Registered only by the integration test host, so the API itself does not change.
/// </summary>
internal sealed class FakeExternalAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    FakeExternalSignIn signIn)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Name of the scheme, used with <c>AuthenticateAsync</c>.</summary>
    public const string SchemeName = "FakeExternal";

    /// <summary>Header that carries the ticket returned by <see cref="FakeExternalSignIn.SignIn"/>.</summary>
    public const string TicketHeader = "X-Fake-External-Ticket";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TicketHeader, out var tickets)
            || !signIn.TryGetAccount(tickets.ToString(), out var account)
            || account is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Subject, ClaimValueTypes.String, account.Provider),
            new(ClaimTypes.Email, account.Email, ClaimValueTypes.Email, account.Provider),
        };
        if (account.Name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, account.Name, ClaimValueTypes.String, account.Provider));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
