# Flatly API

.NET 10 Minimal API with EF Core and PostgreSQL, run locally through .NET Aspire. This page covers the solution layout, running locally, the database and how modules register themselves.

## Layout

```
api/
  Directory.Build.props   Shared compiler and analyzer settings for every project
  global.json             Pins the .NET SDK line
  Flatly.slnx             Solution file
  src/
    Flatly.AppHost/        Aspire AppHost: starts the API and PostgreSQL for local development
    Flatly.ServiceDefaults/ Health checks, OpenTelemetry, resilience and service discovery, shared by hosts
    Flatly.Api/
      Program.cs          Host setup: services, middleware, endpoint mapping
      Data/               The EF Core context, its registration and the migrations
      Modules/
        FlatlyModules.cs  The single list of modules (register and map)
        Platform/         One folder per module, each with <Name>Module.cs
        Flats/
        Chores/
        Shopping/         MVP2
        Calendar/         MVP2
        Maintenance/
```

Module folders map to the product areas in the spec. Platform covers cross-cutting concerns such as accounts and sign-in.

## Local development with Aspire

Aspire runs the API and PostgreSQL together, with one command from `api/`:

```
dotnet run --project src/Flatly.AppHost
```

This needs Docker running (PostgreSQL runs in a container). The Aspire dashboard opens from the console output and shows the resources, logs, traces and health of each one. PostgreSQL keeps its data in a named volume, so it survives restarts of the AppHost.

Aspire is for local development only. Production runs the API on Azure App Service, which does not use the AppHost.

### Health endpoints

Every host calls `AddServiceDefaults()` and `MapDefaultEndpoints()` (see `Flatly.ServiceDefaults`), which map:

- `/health`: readiness. Every health check must pass.
- `/alive`: liveness. Only checks tagged `live` must pass.

Both answer `Healthy` or `Unhealthy`, and are mapped in every environment, since App Service uses them too.

## Database

The API uses one EF Core context, `FlatlyDbContext`, on PostgreSQL. Its conventions apply to every table:

- **Names are snake_case:** tables and columns, for example `created_at`.
- **Timestamps are UTC `timestamptz`.** Npgsql only accepts UTC `DateTime` values for these columns, so a local or unspecified value fails instead of being stored with the wrong offset.

The connection string is `ConnectionStrings:flatly`. Aspire injects it from the `flatly` database in the AppHost, and App Service reads `ConnectionStrings__flatly`.

### Migrations

- **Development** applies pending migrations on every start.
- **Any environment** can apply them explicitly, with the `migrate` command. It applies the pending migrations and exits without starting the server:

  ```
  dotnet run --project src/Flatly.Api -- migrate
  ```

  Deployment runs this command before the new version starts.
- **Add a migration** from `api/src/Flatly.Api`, with the `dotnet-ef` tool at the version of the EF Core packages (10.0.15):

  ```
  dotnet ef migrations add <Name> --output-dir Data/Migrations
  ```

- Migrations are additive only, so the previous version keeps working on the new schema (see [ADR-001](../docs/adr/0001-stack-and-deployment.md)).

The first migration, `Initial`, is empty: the context has no tables yet. The feature tasks add their entities and their migrations.

## Module convention

Each module is a static class `<Name>Module` with three members:

- `RoutePrefix`: the base path of its endpoints, always `/api/v1/<name>`.
- `Add<Name>Module(this IServiceCollection)`: registers the module's services and returns the collection.
- `Map<Name>Module(this IEndpointRouteBuilder)`: maps its endpoints under a `MapGroup(RoutePrefix)` and returns the builder.

`Modules/FlatlyModules.cs` calls every module, in one place. `Program.cs` only calls `AddFlatlyModules()` and `MapFlatlyModules()`, so it does not change when a module is added.

## Adding a module

1. Create `src/Flatly.Api/Modules/<Name>/<Name>Module.cs` following the convention above.
2. Add one line to `AddFlatlyModules` and one to `MapFlatlyModules` in `Modules/FlatlyModules.cs`.
3. Add the new module's folder to the layout above when it is a new product area.
4. Build and run: `dotnet build Flatly.slnx` and `dotnet run --project src/Flatly.AppHost` (or `src/Flatly.Api` on its own, without PostgreSQL). The OpenAPI document is at `/openapi/v1.json`.

## Errors and validation

Every error answers `application/problem+json` (RFC 9457 ProblemDetails):

- **Validation**: attach `ValidationFilter<TRequest>` to an endpoint with `.AddEndpointFilter<ValidationFilter<TRequest>>()` and register one or more `IRequestValidator<TRequest>` in the module's `Add<Name>Module`. Invalid input gets a 400 whose `errors` object is keyed by field name. See `Modules/Platform/Endpoints/ValidationSampleEndpoint.cs` for the pattern.
- **Unhandled exceptions**: `UnhandledExceptionHandler` logs the exception with its trace id and answers 500 with the trace id. The response never contains the exception message or stack trace.

## Example: a vertical slice

A feature lives inside its module folder, so one folder holds everything it needs. This is the pattern for the first real endpoint of a module (for example, creating a flat in `Flats`). The example is documentation only; the code is added with that feature's task.

```
Modules/Flats/
  FlatsModule.cs                 Registration and endpoint group
  Endpoints/CreateFlat.cs        Request, handler and endpoint mapping
  Validation/CreateFlatRequestValidator.cs
  Domain/Flat.cs                 Entity
  Data/FlatsDbContext.cs         EF Core context, owned by this module
  Data/Migrations/               Migrations created from this module's context
```

- **Endpoints**: one file per operation. The file maps its own route inside the module's group, so `FlatsModule.MapFlatsModule` only needs to call the group's mapping methods.
- **Validators**: validate the request before the handler runs. Failures return ProblemDetails.
- **Entities**: plain classes in `Domain/`. They do not depend on HTTP types.
- **Migrations**: each module owns its `DbContext` and migrations, so a module can change its schema without touching others.

Registration for the slice is one extra call inside `AddFlatsModule` (for example `services.AddDbContext<FlatsDbContext>(...)`) and nothing else in `Program.cs`.

## Running

Run `dotnet build Flatly.slnx` from `api/`. CI builds with warnings treated as errors, so keep the build clean locally:

```
GITHUB_ACTIONS=true dotnet build Flatly.slnx -c Release
```

The SDK is pinned in `global.json` with `latestFeature` roll-forward. Package versions are locked in `packages.lock.json` files, which are committed.

The AppHost pins the Aspire SDK version in `Flatly.AppHost.csproj`. Keep the Aspire packages on the same version.
