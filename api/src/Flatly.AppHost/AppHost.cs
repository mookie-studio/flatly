var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container. The named data volume keeps the data across restarts of the AppHost.
// Remove WithDataVolume() for a clean database on every run.
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("flatly", "flatly");

// The API waits for PostgreSQL to accept connections, and is checked through its readiness endpoint.
var api = builder.AddProject<Projects.Flatly_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");

// The Vite dev server for the web app. It starts after the API is healthy, and receives the API address
// through the reference, so web/vite.config.ts can proxy /api to it.
builder.AddViteApp("web", "../../../web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
