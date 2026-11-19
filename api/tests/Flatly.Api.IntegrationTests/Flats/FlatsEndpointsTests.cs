using System.Net;
using System.Net.Http.Json;
using Flatly.Api.Data;
using Flatly.Api.Data.Tenancy;
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

        await FlatIsolation.AssertNotFoundForNonMemberAsync(brunoClient, anasFlat!.Id, id => $"{FlatsUrl}/{id}");
    }

    [SkippableFact]
    public async Task TheFlatFilterReturnsOnlyRowsOfTheFlatInScope()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "scope-ana");
        var bruno = await CreateAccountAsync(factory, "scope-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Scope A"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        var brunosFlat = await (await brunoClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Scope B"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var currentFlat = scope.ServiceProvider.GetRequiredService<CurrentFlat>();

        // No flat in scope: a query that forgot its flat sees nothing, not every flat's rows.
        Assert.Empty(await database.Memberships.Select(membership => membership.FlatId).ToListAsync());

        currentFlat.Id = anasFlat!.Id;
        Assert.Equal(new[] { anasFlat.Id }, await database.Memberships.Select(membership => membership.FlatId).ToListAsync());

        currentFlat.Id = brunosFlat!.Id;
        Assert.Equal(new[] { brunosFlat.Id }, await database.Memberships.Select(membership => membership.FlatId).ToListAsync());
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

    [SkippableFact]
    public async Task ListShowsTheActiveMembersAdminsFirst()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "members-ana");
        var bruno = await CreateAccountAsync(factory, "members-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Members flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member, joinedAt: Start.AddDays(1));

        var members = await anaClient.GetFromJsonAsync<List<FlatMemberResponse>>($"{FlatsUrl}/{flat.Id}/members");

        Assert.Equal(2, members!.Count);
        Assert.Equal(["members-ana", "members-bruno"], members.Select(member => member.DisplayName));
        Assert.Equal(["Admin", "Member"], members.Select(member => member.Role));
    }

    [SkippableFact]
    public async Task ListLeavesOutMembersWhoHaveLeft()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "left-members-ana");
        var bruno = await CreateAccountAsync(factory, "left-members-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Left members flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member, leftAt: Start.AddDays(2));

        var members = await anaClient.GetFromJsonAsync<List<FlatMemberResponse>>($"{FlatsUrl}/{flat.Id}/members");

        Assert.Equal(["left-members-ana"], members!.Select(member => member.DisplayName));
    }

    [SkippableFact]
    public async Task ListOfAnotherFlatAnswersNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "members-iso-ana");
        var bruno = await CreateAccountAsync(factory, "members-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Members iso"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        await FlatIsolation.AssertNotFoundForNonMemberAsync(brunoClient, anasFlat!.Id, id => $"{FlatsUrl}/{id}/members");
    }

    [SkippableFact]
    public async Task AnAdminCanRenameTheFlat()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateSignedInClient(await CreateAccountAsync(factory, "rename-admin"));
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Old name"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var response = await client.PatchAsJsonAsync($"{FlatsUrl}/{flat!.Id}", new UpdateFlatRequest("  New name  "));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var renamed = await response.Content.ReadFromJsonAsync<FlatResponse>();
        Assert.Equal("New name", renamed!.Name);
        Assert.Equal("Admin", renamed.Role);
        Assert.Equal("New name", (await client.GetFromJsonAsync<FlatResponse>($"{FlatsUrl}/{flat.Id}"))!.Name);
    }

    [SkippableFact]
    public async Task AMemberCannotRenameTheFlat()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "rename-owner");
        var bruno = await CreateAccountAsync(factory, "rename-member");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Keep name"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);

        var response = await brunoClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}", new UpdateFlatRequest("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Keep name", (await anaClient.GetFromJsonAsync<FlatResponse>($"{FlatsUrl}/{flat.Id}"))!.Name);
    }

    [SkippableFact]
    public async Task RenamingWithAnInvalidNameIsRejected()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateSignedInClient(await CreateAccountAsync(factory, "rename-invalid"));
        var flat = await (await client.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Valid"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var empty = await client.PatchAsJsonAsync($"{FlatsUrl}/{flat!.Id}", new UpdateFlatRequest(" "));
        var tooLong = await client.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}", new UpdateFlatRequest(new string('x', 81)));

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [SkippableFact]
    public async Task RenamingAFlatOfAnotherMemberAnswersNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "rename-iso-ana");
        var bruno = await CreateAccountAsync(factory, "rename-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Ana rename iso"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var request = new UpdateFlatRequest("Not mine");
        var ofOtherFlat = await brunoClient.PatchAsJsonAsync($"{FlatsUrl}/{anasFlat!.Id}", request);
        var ofMissingFlat = await brunoClient.PatchAsJsonAsync($"{FlatsUrl}/{Guid.NewGuid()}", request);

        Assert.Equal(HttpStatusCode.NotFound, ofOtherFlat.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ofMissingFlat.StatusCode);
        Assert.Equal("Ana rename iso", (await anaClient.GetFromJsonAsync<FlatResponse>($"{FlatsUrl}/{anasFlat.Id}"))!.Name);
    }

    [SkippableFact]
    public async Task AMemberCanLeaveAndLosesAccessToTheFlat()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "leave-ana");
        var bruno = await CreateAccountAsync(factory, "leave-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Leave flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);

        var response = await brunoClient.PostAsync($"{FlatsUrl}/{flat.Id}/leave", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await brunoClient.GetAsync($"{FlatsUrl}/{flat.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await brunoClient.GetAsync($"{FlatsUrl}/{flat.Id}/members")).StatusCode);
        var membership = await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject);
        Assert.Equal(Start, membership.LeftAt);
        Assert.Equal(MemberRole.Member, membership.Role);
    }

    [SkippableFact]
    public async Task TheLastAdminCannotLeaveAFlatThatHasOtherMembers()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "last-admin-ana");
        var bruno = await CreateAccountAsync(factory, "last-admin-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Keep admin"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);

        var response = await anaClient.PostAsync($"{FlatsUrl}/{flat.Id}/leave", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null((await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task TheSoleMemberOfAFlatCanLeaveIt()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "sole-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Sole flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        var response = await anaClient.PostAsync($"{FlatsUrl}/{flat!.Id}/leave", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull((await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task LeavingAFlatYouAreNotInAnswersNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "leave-iso-ana");
        var bruno = await CreateAccountAsync(factory, "leave-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Leave iso"))).Content
            .ReadFromJsonAsync<FlatResponse>();

        await FlatIsolation.AssertNotFoundForNonMemberAsync(
            brunoClient, anasFlat!.Id, id => $"{FlatsUrl}/{id}/leave", HttpMethod.Post);
        Assert.Null((await FindMembershipAsync(factory, anasFlat.Id, ana.Provider, ana.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task AnAdminCanRemoveAMemberWhoKeepsTheirHistory()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "remove-ana");
        var bruno = await CreateAccountAsync(factory, "remove-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Remove flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);

        var response = await anaClient.DeleteAsync($"{FlatsUrl}/{flat.Id}/members/{(await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).UserId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await brunoClient.GetAsync($"{FlatsUrl}/{flat.Id}")).StatusCode);
        var membership = await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject);
        Assert.Equal(Start, membership.LeftAt);
        Assert.Equal(["remove-ana"], (await anaClient.GetFromJsonAsync<List<FlatMemberResponse>>($"{FlatsUrl}/{flat.Id}/members"))!
            .Select(member => member.DisplayName));
    }

    [SkippableFact]
    public async Task AMemberCannotRemoveAnotherMember()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "remove-owner");
        var bruno = await CreateAccountAsync(factory, "remove-member-bruno");
        var carla = await CreateAccountAsync(factory, "remove-member-carla");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("No removals"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);
        await AddMemberAsync(factory, flat.Id, carla, MemberRole.Member);
        var carlaId = (await FindMembershipAsync(factory, flat.Id, carla.Provider, carla.Subject)).UserId;

        var response = await brunoClient.DeleteAsync($"{FlatsUrl}/{flat.Id}/members/{carlaId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null((await FindMembershipAsync(factory, flat.Id, carla.Provider, carla.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task AnAdminCannotRemoveThemselvesAndNonMembersAreNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "self-remove-ana");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Self remove"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        var anaId = (await FindMembershipAsync(factory, flat!.Id, ana.Provider, ana.Subject)).UserId;

        var self = await anaClient.DeleteAsync($"{FlatsUrl}/{flat.Id}/members/{anaId}");
        var unknown = await anaClient.DeleteAsync($"{FlatsUrl}/{flat.Id}/members/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Null((await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task RemovingFromAFlatOfAnotherMemberAnswersNotFound()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "remove-iso-ana");
        var bruno = await CreateAccountAsync(factory, "remove-iso-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var anasFlat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Remove iso"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        var anaId = (await FindMembershipAsync(factory, anasFlat!.Id, ana.Provider, ana.Subject)).UserId;

        await FlatIsolation.AssertNotFoundForNonMemberAsync(
            brunoClient, anasFlat.Id, id => $"{FlatsUrl}/{id}/members/{anaId}", HttpMethod.Delete);
        Assert.Null((await FindMembershipAsync(factory, anasFlat.Id, ana.Provider, ana.Subject)).LeftAt);
    }

    [SkippableFact]
    public async Task AFormerMemberCanRejoinWithANewMembership()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "rejoin-ana");
        var bruno = await CreateAccountAsync(factory, "rejoin-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Rejoin flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member, leftAt: Start.AddDays(1));

        await AddMemberAsync(factory, flat.Id, bruno, MemberRole.Member, joinedAt: Start.AddDays(2));

        Assert.Equal(HttpStatusCode.OK, (await brunoClient.GetAsync($"{FlatsUrl}/{flat.Id}")).StatusCode);
    }

    [SkippableFact]
    public async Task AnAdminCanPromoteAMemberAndTheListShowsTheNewRole()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "role-ana");
        var bruno = await CreateAccountAsync(factory, "role-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Promote flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);
        var brunoId = (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).UserId;

        var response = await anaClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{brunoId}", new ChangeMemberRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(MemberRole.Admin, (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).Role);
        var members = await anaClient.GetFromJsonAsync<List<FlatMemberResponse>>($"{FlatsUrl}/{flat.Id}/members");
        Assert.Equal(["Admin", "Admin"], members!.Select(member => member.Role));
    }

    [SkippableFact]
    public async Task AnAdminCanDemoteAnotherAdminWhileAnotherAdminRemains()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "demote-ana");
        var bruno = await CreateAccountAsync(factory, "demote-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Demote flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Admin);
        var brunoId = (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).UserId;

        var response = await anaClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{brunoId}", new ChangeMemberRoleRequest("Member"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(MemberRole.Member, (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).Role);
    }

    [SkippableFact]
    public async Task TheSoleAdminCannotBeDemotedUntilAnotherMemberIsPromoted()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "sole-ana");
        var bruno = await CreateAccountAsync(factory, "sole-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Sole admin flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);
        var anaId = (await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject)).UserId;

        var demote = await anaClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{anaId}", new ChangeMemberRoleRequest("Member"));

        Assert.Equal(HttpStatusCode.Conflict, demote.StatusCode);
        Assert.Equal(MemberRole.Admin, (await FindMembershipAsync(factory, flat.Id, ana.Provider, ana.Subject)).Role);
    }

    [SkippableFact]
    public async Task AMemberCannotChangeRoles()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "role-owner");
        var bruno = await CreateAccountAsync(factory, "role-member-bruno");
        var carla = await CreateAccountAsync(factory, "role-member-carla");
        using var brunoClient = factory.CreateSignedInClient(bruno);
        var flat = await (await factory.CreateSignedInClient(ana).PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("No role changes")))
            .Content.ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);
        await AddMemberAsync(factory, flat.Id, carla, MemberRole.Member);
        var carlaId = (await FindMembershipAsync(factory, flat.Id, carla.Provider, carla.Subject)).UserId;

        var response = await brunoClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{carlaId}", new ChangeMemberRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MemberRole.Member, (await FindMembershipAsync(factory, flat.Id, carla.Provider, carla.Subject)).Role);
    }

    [SkippableFact]
    public async Task AnInvalidRoleAndAnUnknownMemberAreRejected()
    {
        await using var factory = await CreateFactoryAsync();
        var ana = await CreateAccountAsync(factory, "role-invalid-ana");
        var bruno = await CreateAccountAsync(factory, "role-invalid-bruno");
        using var anaClient = factory.CreateSignedInClient(ana);
        var flat = await (await anaClient.PostAsJsonAsync(FlatsUrl, new CreateFlatRequest("Invalid role flat"))).Content
            .ReadFromJsonAsync<FlatResponse>();
        await AddMemberAsync(factory, flat!.Id, bruno, MemberRole.Member);
        var brunoId = (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).UserId;

        var invalid = await anaClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{brunoId}", new ChangeMemberRoleRequest("Owner"));
        var unknown = await anaClient.PatchAsJsonAsync($"{FlatsUrl}/{flat.Id}/members/{Guid.NewGuid()}", new ChangeMemberRoleRequest("Admin"));

        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(MemberRole.Member, (await FindMembershipAsync(factory, flat.Id, bruno.Provider, bruno.Subject)).Role);
    }

    private static async Task<FakeExternalAccount> CreateAccountAsync(FlatlyApiFactory factory, string name)
    {
        var account = new FakeExternalAccount(ExternalProviders.Google, $"{name}-{Guid.NewGuid():N}", $"{name}@example.com", name);
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        await accounts.FindOrCreateAsync(new ExternalIdentity(account.Provider, account.Subject, account.Email, account.Name));
        return account;
    }

    private static async Task AddMemberAsync(
        FlatlyApiFactory factory,
        Guid flatId,
        FakeExternalAccount account,
        MemberRole role,
        DateTimeOffset? leftAt = null,
        DateTimeOffset? joinedAt = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        database.Memberships.Add(new Membership
        {
            Id = Guid.NewGuid(),
            FlatId = flatId,
            UserId = login.UserId,
            Role = role,
            JoinedAt = joinedAt ?? Start,
            LeftAt = leftAt,
        });
        await database.SaveChangesAsync();
    }

    private static async Task<Membership> FindMembershipAsync(FlatlyApiFactory factory, Guid flatId, string provider, string subject)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == provider && item.ProviderKey == subject);
        // Test setup reads across flats, so it bypasses the flat filter on purpose.
        return await database.Memberships.IgnoreQueryFilters()
            .SingleAsync(membership => membership.FlatId == flatId && membership.UserId == login.UserId);
    }

    private static async Task LeaveFlatAsync(FlatlyApiFactory factory, Guid flatId, FakeExternalAccount account)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<FlatlyDbContext>();
        var login = await database.UserLogins.SingleAsync(item => item.LoginProvider == account.Provider && item.ProviderKey == account.Subject);
        var membership = await database.Memberships.IgnoreQueryFilters()
            .SingleAsync(item => item.FlatId == flatId && item.UserId == login.UserId);
        membership.LeftAt = Start;
        await database.SaveChangesAsync();
    }

    private async Task<FlatlyApiFactory> CreateFactoryAsync()
    {
        Skip.IfNot(await DatabaseAvailability.IsAvailableAsync(), DatabaseAvailability.SkipReason);
        return new FlatlyApiFactory(await postgres.ConnectionStringAsync(), Start);
    }
}
