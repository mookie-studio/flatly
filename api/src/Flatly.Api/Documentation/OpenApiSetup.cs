using Scalar.AspNetCore;

namespace Flatly.Api.Documentation;

/// <summary>
/// The OpenAPI document and the Scalar UI. The document is served in every environment, because the frontend
/// generates its typed client from it. Scalar is served only in Development and Staging, never in Production.
/// </summary>
/// <remarks>
/// Operation ids come from endpoint names: every endpoint under <c>/api/</c> must call <c>WithName</c> with a unique
/// name. Renaming one is a breaking change for the generated client. See <c>api/README.md</c>.
/// </remarks>
public static class OpenApiSetup
{
    /// <summary>Registers the OpenAPI document generator.</summary>
    public static IServiceCollection AddFlatlyOpenApi(this IServiceCollection services) => services.AddOpenApi();

    /// <summary>Maps <c>/openapi/v1.json</c> and, outside Production, the Scalar UI at <c>/scalar</c>.</summary>
    public static WebApplication MapFlatlyOpenApi(this WebApplication app)
    {
        app.MapOpenApi();

        if (app.Environment.IsDevelopment() || app.Environment.IsStaging())
        {
            app.MapScalarApiReference(options => options.WithTitle("Flatly API"));
        }

        return app;
    }
}
