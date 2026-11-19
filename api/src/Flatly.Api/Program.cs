using Flatly.Api.Modules;

var builder = WebApplication.CreateBuilder(args);

// Health checks, OpenTelemetry, resilience and service discovery. See Flatly.ServiceDefaults.
builder.AddServiceDefaults();

// Every error the framework writes (validation, unhandled exceptions) becomes application/problem+json.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddFlatlyModules();

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.MapOpenApi();
app.MapFlatlyModules();

app.Run();
