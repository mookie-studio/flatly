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

app.Run();
