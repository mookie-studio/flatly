var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL in a container. The named data volume keeps the data across restarts of the AppHost.
// Remove WithDataVolume() for a clean database on every run.
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("flatly", "flatly");

// Mailpit catches every email the API sends, so nothing leaves the machine. Its web UI is on the "http" endpoint.
var mailpit = builder.AddContainer("mailpit", "axllent/mailpit")
    .WithHttpEndpoint(targetPort: 8025, name: "http")
    .WithEndpoint(targetPort: 1025, name: "smtp");
var smtp = mailpit.GetEndpoint("smtp");

// The API waits for PostgreSQL and Mailpit, and is checked through its readiness endpoint.
// It sends email through SMTP to Mailpit; deployment uses Resend instead (see api/README.md).
var api = builder.AddProject<Projects.Flatly_Api>("api")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(mailpit)
    .WithEnvironment("Email__Provider", "Smtp")
    .WithEnvironment("Email__Smtp__Host", smtp.Property(EndpointProperty.Host))
    .WithEnvironment("Email__Smtp__Port", smtp.Property(EndpointProperty.Port))
    .WithHttpHealthCheck("/health");

// The Vite dev server for the web app. It starts after the API is healthy, and receives the API address
// through the reference, so web/vite.config.ts can proxy /api to it.
builder.AddViteApp("web", "../../../web")
    .WithReference(api)
    .WaitFor(api);

builder.Build().Run();
