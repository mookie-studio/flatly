using System.Net;
using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.IntegrationTests.Fakes;
using Flatly.Api.IntegrationTests.Infrastructure;
using Flatly.Api.Modules.Flats.Domain;
using Flatly.Api.Modules.Flats.Endpoints;
using Flatly.Api.Modules.Notifications.Domain;
using Flatly.Api.Modules.Notifications.Endpoints;
using Flatly.Api.Modules.Notifications.Services;
using Flatly.Api.Modules.Platform.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Flatly.Api.IntegrationTests.Notifications;

/// <summary>
/// Notifies members through the internal service and reads the inbox over HTTP, against the real PostgreSQL.
/// Every test uses its own provider subjects, because all tests in the run share one database.
/// </summary>
[Collection(SharedPostgres.Name)]
public sealed class NotificationsEndpointsTests(PostgresContainer postgres)
{
    private static readonly DateTimeOffset Start = new(2026, 11, 19, 9, 0, 0, TimeSpan.Zero);
    private const string FlatsUrl = "/api/v1/flats";
    private const string InboxUrl = "/api/v1/notifications";
    private const string DueTodayKey = "chores.due_today";
    private static readonly string[] NewestFirstTexts = ["La tarea \"Kitchen\" vence hoy.", "La tarea \"Bathroom\" vence hoy."];
    private static readonly string[] SecondPageTexts = ["La tarea \"Task 1\" vence hoy."];

    [SkippableFact]
    public async Task AMemberSeesOwnNotificationsNewestFirst()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "inbox-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Inbox flat");
        await NotifyAsync(factory, flatId, [await UserIdAsync(factory, ana)], "Bathroom", minutesAfterStart: 1);
        await NotifyAsync(factory, flatId, [await UserIdAsync(factory, ana)], "Kitchen", minutesAfterStart: 2);

        var page = await client.GetFromJsonAsync<NotificationPageResponse>(InboxUrl);

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(ListNotificationsEndpoint.DefaultPageSize, page.PageSize);
        Assert.Equal(NewestFirstTexts, page.Items.Select(item => item.Text));
        Assert.All(page.Items, item => Assert.Equal("Chores", item.Category));
        Assert.All(page.Items, item => Assert.Null(item.ReadAt));
    }

    [SkippableFact]
    public async Task TheTextIsRenderedInTheRecipientsLanguage()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "language-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Language flat");
        var anaId = await UserIdAsync(factory, ana);
        await NotifyAsync(factory, flatId, [anaId], "Bins");

        await SetLanguageAsync(factory, anaId, UserLanguages.Spanish);
        var spanish = await client.GetFromJsonAsync<NotificationPageResponse>(InboxUrl);
        await SetLanguageAsync(factory, anaId, UserLanguages.English);
        var english = await client.GetFromJsonAsync<NotificationPageResponse>(InboxUrl);

        Assert.Equal("La tarea \"Bins\" vence hoy.", spanish!.Items.Single().Text);
        Assert.Equal("The chore \"Bins\" is due today.", english!.Items.Single().Text);
    }

    [SkippableFact]
    public async Task UnreadCountAndUnreadFilterFollowMarkingRead()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "unread-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Unread flat");
        var anaId = await UserIdAsync(factory, ana);
        await NotifyAsync(factory, flatId, [anaId], "First", minutesAfterStart: 1);
        await NotifyAsync(factory, flatId, [anaId], "Second", minutesAfterStart: 2);
        var first = (await client.GetFromJsonAsync<NotificationPageResponse>(InboxUrl))!.Items[^1];

        var marked = await client.PostAsync($"{InboxUrl}/{first.Id}/read", null);
        var again = await client.PostAsync($"{InboxUrl}/{first.Id}/read", null);
        var count = await client.GetFromJsonAsync<UnreadCountResponse>($"{InboxUrl}/unread-count");
        var unread = await client.GetFromJsonAsync<NotificationPageResponse>($"{InboxUrl}?unread=true");

        Assert.Equal(HttpStatusCode.NoContent, marked.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
        Assert.Equal(1, count!.Count);
        Assert.Equal(1, unread!.TotalCount);
        Assert.DoesNotContain(unread.Items, item => item.Id == first.Id);
    }

    [SkippableFact]
    public async Task MarkingAllAsReadEmptiesTheUnreadCount()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "read-all-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Read all flat");
        var anaId = await UserIdAsync(factory, ana);
        await NotifyAsync(factory, flatId, [anaId], "One");
        await NotifyAsync(factory, flatId, [anaId], "Two");

        var response = await client.PostAsync($"{InboxUrl}/read-all", null);
        var count = await client.GetFromJsonAsync<UnreadCountResponse>($"{InboxUrl}/unread-count");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, count!.Count);
    }

    [SkippableFact]
    public async Task AnotherMembersNotificationIsInvisibleAndCannotBeMarked()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "isolation-ana");
        var bruno = await CreateAccountAsync(factory, "isolation-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flatId = await CreateFlatAsync(anaClient, "Shared flat");
        await AddMemberAsync(factory, flatId, bruno);
        await NotifyAsync(factory, flatId, [await UserIdAsync(factory, ana)], "Ana only");
        var anaNotification = (await anaClient.GetFromJsonAsync<NotificationPageResponse>(InboxUrl))!.Items.Single();

        var brunoInbox = await brunoClient.GetFromJsonAsync<NotificationPageResponse>(InboxUrl);
        var brunoMarks = await brunoClient.PostAsync($"{InboxUrl}/{anaNotification.Id}/read", null);
        var anaStillUnread = await anaClient.GetFromJsonAsync<UnreadCountResponse>($"{InboxUrl}/unread-count");

        Assert.Equal(0, brunoInbox!.TotalCount);
        Assert.Equal(HttpStatusCode.NotFound, brunoMarks.StatusCode);
        Assert.Equal(1, anaStillUnread!.Count);
    }

    [SkippableFact]
    public async Task NotificationsAboutAFlatTheCallerLeftAreNotShown()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "former-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Left flat");
        await NotifyAsync(factory, flatId, [await UserIdAsync(factory, ana)], "Old news");
        await LeaveFlatAsync(factory, flatId, ana);

        var page = await client.GetFromJsonAsync<NotificationPageResponse>(InboxUrl);
        var count = await client.GetFromJsonAsync<UnreadCountResponse>($"{InboxUrl}/unread-count");

        Assert.Equal(0, page!.TotalCount);
        Assert.Equal(0, count!.Count);
    }

    [SkippableFact]
    public async Task PagingReturnsTheRequestedPage()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "paging-ana");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Paging flat");
        var anaId = await UserIdAsync(factory, ana);
        for (var index = 1; index <= 3; index++)
        {
            await NotifyAsync(factory, flatId, [anaId], $"Task {index}", minutesAfterStart: index);
        }

        var second = await client.GetFromJsonAsync<NotificationPageResponse>($"{InboxUrl}?page=2&pageSize=2");
        var invalid = await client.GetAsync($"{InboxUrl}?pageSize=0");

        Assert.Equal(3, second!.TotalCount);
        Assert.Equal(SecondPageTexts, second.Items.Select(item => item.Text));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [SkippableFact]
    public async Task NotifyingAStrangerOrAnUnknownKeyIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "guard-ana");
        var stranger = await CreateAccountAsync(factory, "guard-stranger");
        using var client = factory.CreateSignedInClient(ana);
        var flatId = await CreateFlatAsync(client, "Guarded flat");

        var anaId = await UserIdAsync(factory, ana);
        var strangerId = await UserIdAsync(factory, stranger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => NotifyAsync(factory, flatId, [anaId, strangerId], "Nope"));
        await Assert.ThrowsAsync<ArgumentException>(() => NotifyAsync(factory, flatId, [anaId], "Unknown", key: "chores.not_a_key"));
    }

    [SkippableFact]
    public async Task RequestsWithoutASignInAreRejected()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();

        var inbox = await client.GetAsync(InboxUrl);
        var count = await client.GetAsync($"{InboxUrl}/unread-count");
        var readAll = await client.PostAsync($"{InboxUrl}/read-all", null);

        Assert.Equal(HttpStatusCode.Unauthorized, inbox.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, count.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, readAll.StatusCode);
    }

    private static async Task<Guid> CreateFlatAsync(HttpClient client, string name)
    {
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest(name))).Content
            .ReadFromJsonAsync<FlatResponse>();
        return flat!.Id;
    }

    /// <summary>Notifies the recipients through the internal service, the way a module does.</summary>
    private static async Task NotifyAsync(
        FlatlyApiFactory factory,
        Guid flatId,
        IReadOnlyCollection<Guid> recipients,
        string task,
        int minutesAfterStart = 0,
        string key = DueTodayKey)
    {
        factory.Clock.SetUtcNow(Start.AddMinutes(minutesAfterStart));
        await using var scope = factory.Services.CreateAsyncScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notifications.NotifyNowAsync(new NotificationRequest(
            flatId,
            recipients,
            NotificationCategory.Chores,
            key,
            new Dictionary<string, string> { ["task"] = task }));
    }

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

    private static async Task SetLanguageAsync(FlatlyApiFactory factory, Guid userId, string language)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var user = await database.Users.SingleAsync(item => item.Id == userId);
        user.PreferredLanguage = language;
        await database.SaveChangesAsync();
    }

    private static async Task AddMemberAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        var userId = await UserIdAsync(factory, account);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            UserId = userId,
            Role = MemberRole.Member,
            JoinedAt = Start,
        });
        await database.SaveChangesAsync();
    }

    private static async Task LeaveFlatAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        var userId = await UserIdAsync(factory, account);
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var membership = await database.Memberships.SingleAsync(item => item.FlatId == flatId && item.UserId == userId);
        membership.LeftAt = Start;
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
