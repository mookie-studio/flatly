using System.Security.Claims;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.Modules.Platform.Email;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Checks the fakes the other integration tests rely on: the email outbox and the fake external sign-in.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class FakesTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);

    private static readonly FakeExternalAccount Ana = new("Google", "google-subject-1", "ana@example.com", "Ana");

    [SkippableFact]
    public async Task EmailsSentThroughTheApiAreKeptInTheOutbox()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var message = new EmailMessage("ana@example.com", "Bins night", "Take the bins out on Thursday.");

        await sender.SendAsync(message);

        Assert.Same(factory.Email, sender);
        Assert.Equal([message], factory.Email.Outbox);
    }

    [SkippableFact]
    public async Task TheOutboxKeepsMessagesInTheOrderTheyWereSent()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        await sender.SendAsync(new EmailMessage("ana@example.com", "First", "one"));
        await sender.SendAsync(new EmailMessage("bruno@example.com", "Second", "two"));

        Assert.Equal(["First", "Second"], factory.Email.Outbox.Select(message => message.Subject));
    }

    [SkippableFact]
    public async Task ClearingTheOutboxRemovesEarlierMessages()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        await sender.SendAsync(new EmailMessage("ana@example.com", "Old", "old"));

        factory.Email.Clear();

        Assert.Empty(factory.Email.Outbox);
    }

    [SkippableFact]
    public async Task ATicketFromTheFakeProviderSignsInTheGivenAccount()
    {
        await using var factory = await CreateFactoryAsync();
        var ticket = factory.ExternalSignIn.SignIn(Ana);

        var result = await AuthenticateWithTicketAsync(factory, ticket);

        Assert.True(result.Succeeded);
        Assert.Equal(FakeExternalAuthenticationHandler.SchemeName, result.Ticket!.AuthenticationScheme);
        Assert.Equal(Ana.Subject, result.Principal!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        Assert.Equal(Ana.Email, result.Principal.FindFirst(ClaimTypes.Email)!.Value);
        Assert.Equal(Ana.Name, result.Principal.FindFirst(ClaimTypes.Name)!.Value);
    }

    [SkippableFact]
    public async Task RequestWithoutATicketIsNotSignedIn()
    {
        await using var factory = await CreateFactoryAsync();

        var result = await AuthenticateWithTicketAsync(factory, ticket: null);

        Assert.False(result.Succeeded);
    }

    [SkippableFact]
    public async Task AnUnknownTicketIsNotSignedIn()
    {
        await using var factory = await CreateFactoryAsync();

        var result = await AuthenticateWithTicketAsync(factory, "not-a-ticket");

        Assert.False(result.Succeeded);
    }

    private static async Task<AuthenticateResult> AuthenticateWithTicketAsync(FlatlyApiFactory factory, string? ticket)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        if (ticket is not null)
        {
            context.Request.Headers[FakeExternalAuthenticationHandler.TicketHeader] = ticket;
        }

        var authentication = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        return await authentication.AuthenticateAsync(context, FakeExternalAuthenticationHandler.SchemeName);
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
