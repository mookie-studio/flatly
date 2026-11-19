using Flatly.Api.Modules.Platform.Identity;
using Xunit;

namespace Flatly.Api.Tests.Identity;

/// <summary>Checks that the sign-in return address can only lead to the web app's own origin.</summary>
public sealed class ReturnUrlTests
{
    private const string WebOrigin = "https://web.test";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MissingReturnUrlGoesToTheWebHomePage(string? returnUrl) =>
        Assert.Equal("https://web.test/", ReturnUrl.Resolve(returnUrl, WebOrigin));

    [Theory]
    [InlineData("/flats", "https://web.test/flats")]
    [InlineData("/flats?tab=members", "https://web.test/flats?tab=members")]
    [InlineData("https://web.test/flats", "https://web.test/flats")]
    [InlineData("https://web.test/flats?x=1", "https://web.test/flats?x=1")]
    public void PathsAndAddressesOnTheWebOriginAreKept(string returnUrl, string expected) =>
        Assert.Equal(expected, ReturnUrl.Resolve(returnUrl, WebOrigin));

    [Theory]
    [InlineData("//evil.example/steal")]
    [InlineData("/\\evil.example/steal")]
    [InlineData("https://evil.example/")]
    [InlineData("https://web.test.evil.example/")]
    [InlineData("http://evil.example/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("flats")]
    public void AnythingElseGoesToTheWebHomePage(string returnUrl) =>
        Assert.Equal("https://web.test/", ReturnUrl.Resolve(returnUrl, WebOrigin));

    [Fact]
    public void WithoutAConfiguredOriginOnlyPathsAreKept()
    {
        Assert.Equal("/flats", ReturnUrl.Resolve("/flats", null));
        Assert.Equal("/", ReturnUrl.Resolve("https://evil.example/", null));
    }

    [Fact]
    public void AnInvalidConfiguredOriginIsIgnored() =>
        Assert.Equal("/", ReturnUrl.Resolve("https://web.test/flats", "not a url"));
}
