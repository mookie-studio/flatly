using Flatly.Api.Data;
using Flatly.Api.Errors;
using Flatly.Api.Modules;

var builder = WebApplication.CreateBuilder(args);

// Health checks, OpenTelemetry, resilience and service discovery. See Flatly.ServiceDefaults.
builder.AddServiceDefaults();

// Every error becomes application/problem+json: framework errors, validation (see Validation/ValidationFilter)
// and unhandled exceptions (see Errors/UnhandledExceptionHandler).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<UnhandledExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddFlatlyModules();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
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
