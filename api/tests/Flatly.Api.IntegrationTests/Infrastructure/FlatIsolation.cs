using System.Net;
using Xunit;

namespace Flatly.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Shared check for ADR-002: an endpoint under the flat route must hide a flat from accounts that are not its members.
/// Every flat-scoped endpoint test calls it with a signed-in non-member.
/// </summary>
public static class FlatIsolation
{
    /// <summary>
    /// Asserts that <paramref name="nonMember"/> gets 404 for the flat <paramref name="flatId"/>, and the same 404 for
    /// a flat that does not exist, so the existence of other flats is not revealed.
    /// </summary>
    /// <param name="nonMember">A client signed in as an account with no active membership in the flat.</param>
    /// <param name="flatId">The flat that belongs to someone else.</param>
    /// <param name="routeOfFlat">Builds the endpoint's route for a flat id, for example <c>id =&gt; $"/api/v1/flats/{id}/chores"</c>.</param>
    /// <param name="method">HTTP method of the endpoint. GET when omitted.</param>
    public static async Task AssertNotFoundForNonMemberAsync(
        HttpClient nonMember,
        Guid flatId,
        Func<Guid, string> routeOfFlat,
        HttpMethod? method = null)
    {
        var verb = method ?? HttpMethod.Get;

        var ofOtherFlat = await nonMember.SendAsync(new HttpRequestMessage(verb, routeOfFlat(flatId)));
        var ofMissingFlat = await nonMember.SendAsync(new HttpRequestMessage(verb, routeOfFlat(Guid.NewGuid())));

        Assert.Equal(HttpStatusCode.NotFound, ofOtherFlat.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ofMissingFlat.StatusCode);
    }
}
