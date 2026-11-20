using Flatly.Api.Data;
using Flatly.Api.Documentation;
using Flatly.Api.Errors;
using Flatly.Api.Modules;
using Flatly.Api.Modules.Platform.Scheduling;

var builder = WebApplication.CreateBuilder(args);

// Health checks, OpenTelemetry, resilience and service discovery. See Flatly.ServiceDefaults.
builder.AddServiceDefaults();

// Every error becomes application/problem+json: framework errors, validation (see Validation/ValidationFilter)
// and unhandled exceptions (see Errors/UnhandledExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddFlatlyOpenApi();
builder.Services.AddFlatlyModules();

var app = builder.Build();

app.UseExceptionHandler();
// Before authentication: the dashboard has its own check (Modules/Platform/Scheduling), and the default-deny fallback
// policy would otherwise answer 401 to it. Served in Development only.
if (app.Environment.IsDevelopment())
{
    app.UseJobDashboard();
}
// Authentication before authorization, and both before the endpoints. Every endpoint needs a signed-in member unless it is AllowAnonymous.
app.UseAuthentication();
app.UseAuthorization();
// After authentication, so the join-flat rate limit can tell accounts apart (see Modules/Flats/FlatsModule).
app.UseRateLimiter();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapFlatlyOpenApi();
app.MapFlatlyModules();

// `dotnet run --project src/Flatly.Api -- migrate` applies the pending migrations and exits. Deployment runs it before the new version starts.
if (args is ["migrate"])
{
    await app.MigrateDatabaseAsync();
    return;
}

// Local development brings the schema up to date on every start. Other environments only migrate through the command above.
if (app.Environment.IsDevelopment())
{
    await app.MigrateDatabaseAsync();
}

app.Run();

// Makes Program visible to the integration tests, which host the API in-process (see Flatly.Api.IntegrationTests).
public partial class Program;
