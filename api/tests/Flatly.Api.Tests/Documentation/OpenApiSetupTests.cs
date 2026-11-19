using Xunit;
using Flatly.Api.Documentation;
using Flatly.Api.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Flatly.Api.Tests.Documentation;

public class OpenApiSetupTests
{
    [Theory]
    [InlineData("Development", true)]
    [InlineData("Staging", true)]
    [InlineData("Production", false)]
    public async Task ScalarUiIsServedOnlyOutsideProduction(string environment, bool expected)
    {
        await using var app = await BuildAppAsync(environment);

        Assert.Equal(expected, HasRoute(app, "scalar"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task OpenApiDocumentIsServedInEveryEnvironment(string environment)
    {
        await using var app = await BuildAppAsync(environment);

        Assert.True(HasRoute(app, "openapi/{documentName}.json"));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task EveryApiEndpointHasAUniqueOperationId(string environment)
    {
        await using var app = await BuildAppAsync(environment);

        var names = Endpoints(app)
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .Select(e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName)
            .ToList();

        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.False(string.IsNullOrWhiteSpace(name)));
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static Task<WebApplication> BuildAppAsync(string environment)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environment,
            Args = [],
        });
        builder.Services.AddFlatlyOpenApi();
        builder.Services.AddFlatlyModules();

        var app = builder.Build();
        app.MapFlatlyOpenApi();
        app.MapFlatlyModules();
        return Task.FromResult(app);
    }

    private static IEnumerable<RouteEndpoint> Endpoints(WebApplication app) =>
        ((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

    private static bool HasRoute(WebApplication app, string fragment) =>
        Endpoints(app).Any(e => e.RoutePattern.RawText?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true);
}
