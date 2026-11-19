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
  tests/
    Flatly.Api.Tests/             Unit tests: pure code and database conventions, no server
    Flatly.Api.IntegrationTests/  The API in-process against a real PostgreSQL
```

Module folders map to the product areas in the spec. Platform covers cross-cutting concerns such as accounts and sign-in.

## Local development with Aspire

Aspire runs PostgreSQL, the API and the web app (`web/`, the Vite dev server) together, with one command from `api/`:

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

The migration `Initial` is empty. `AddIdentity` creates the account tables: `users` (with the profile fields `display_name`, `avatar_url`, `preferred_language` and `time_zone_id`), `user_logins` (one row per linked Google or Microsoft account), `user_claims` and `user_tokens`. `AddFlats` creates `flats` and `memberships` (one row per member and flat, with `left_at` set when a member leaves). The feature tasks add their entities and their migrations.

### Flats and memberships

`Modules/Flats` holds the tenant of the product (ADR-002). A flat has a name and a creation time. A membership links a user to a flat with a role (`Admin` or `Member`), a join time and an optional leave time. Leaving keeps the row. The creator of a flat becomes its admin.

The endpoints are `POST /api/v1/flats`, `GET /api/v1/flats` (the caller's active flats) and `GET /api/v1/flats/{flatId}`. A flat the caller does not belong to answers 404. Tenant scoping for other modules is added by the next task (#76).

Endpoints identify the caller with `CurrentAccountResolver` (`Modules/Platform/Identity`). It reads the provider and subject from the signed-in principal and returns 401 when nobody is signed in. Production has no authentication middleware yet, so these endpoints answer 401 until the sign-in task adds it. The integration tests add the middleware and sign in with the fake provider (`FlatlyApiFactory.CreateSignedInClient`).

### Accounts

Accounts use ASP.NET Core Identity on `FlatlyDbContext`, without passwords (see [ADR-003](../docs/adr/0003-identity-with-external-providers.md)). `ExternalAccountService.FindOrCreateAsync` returns the account behind an external sign-in, and creates it the first time. Accounts are matched only by provider and subject id, never by email, so the same email from Google and Microsoft gives two accounts. Sign-in is Google only for now. The endpoints live in `Modules/Platform/Endpoints/AuthEndpoints.cs`:

- `GET /api/v1/platform/auth/google/sign-in?returnUrl=` starts the Google sign-in (the provider's callback is `/signin-google`). `returnUrl` may only point to the web origin (`Web:Origin`); anything else goes to its home page.
- `GET /api/v1/platform/auth/session` returns the signed-in member, or 401.
- `GET /api/v1/platform/auth/antiforgery-token` issues the token that every state-changing request sends in the `X-XSRF-TOKEN` header. `POST /api/v1/platform/auth/sign-out` requires it.

The session is an HttpOnly, Secure, `SameSite=None` cookie (`Flatly.Session`) issued after the callback, because the web app and the API are on different sites. Its principal's name identifier is the Flatly account id.

**Default-deny:** the fallback authorization policy requires a signed-in member on every endpoint. To make an endpoint public, mark it `.AllowAnonymous()` explicitly. Today that is the sign-in, token and sign-out endpoints, the health probes, the OpenAPI document and Scalar, and the two sample endpoints. Integration tests replace the cookie with the fake provider's ticket as the default scheme (see `FlatlyApiFactory`).

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
4. Build and run: `dotnet build Flatly.slnx` and `dotnet run --project src/Flatly.AppHost` (or `src/Flatly.Api` on its own, without PostgreSQL). The OpenAPI document is at `/openapi/v1.json`. Scalar, the API reference UI, is at `/scalar` in Development and Staging only; it is not served in Production.

## OpenAPI and operation ids

The OpenAPI document is generated from the endpoints, and the frontend generates its typed client from it. Every endpoint under `/api/` must call `.WithName("...")` with a unique name, because that name becomes the `operationId`. Renaming an endpoint changes its operation id, which breaks the generated client, so rename one only together with the client that uses it. `Documentation/OpenApiSetup.cs` holds the registration, and `Documentation/OpenApiSetupTests.cs` fails when two endpoints share a name.

## Email

Notifications send email through `IEmailSender` (`Modules/Platform/Email`). Features depend on the interface, never on a provider, and tests replace it with `FakeEmailSender`.

- **Local development**: the AppHost starts Mailpit, and the API sends through SMTP to it. Open Mailpit's web UI from the Aspire dashboard to read the messages. Nothing leaves the machine.
- **Deployment**: `Email__Provider=Resend`, with the sender domain verified in Resend. The API key is a secret: set `Email__Resend__ApiKey` in App Service settings, never in the repository.
- **Settings** (section `Email`): `Provider` (`Smtp` or `Resend`), `FromAddress`, `FromName`, `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl`, `Smtp:UserName`, `Smtp:Password`, `Resend:ApiKey`, `Resend:BaseUrl`. A missing sender address, an unknown provider or a missing Resend key stops the API at startup.
- **Templates**: `EmailTemplates.Notification` renders a notification in Spanish or English from the member's `preferred_language`. Unknown or missing languages fall back to Spanish.

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

## Tests

- **Unit tests** (`tests/Flatly.Api.Tests`) cover pure code and the database conventions. They need no server: `dotnet test tests/Flatly.Api.Tests`.
- **Integration tests** (`tests/Flatly.Api.IntegrationTests`) host the real API in-process with `WebApplicationFactory` and run it against PostgreSQL 17, started by Testcontainers. They need Docker running. Without Docker they are skipped, and the skip message says why. To use an existing PostgreSQL instead, set `FLATLY_TEST_CONNECTION_STRING`; the tests migrate that database, so give them a database of their own.

  ```
  dotnet test tests/Flatly.Api.IntegrationTests
  ```

### Time

Code reads the current time from `TimeProvider`, never from `DateTime.UtcNow`. The Platform module registers `TimeProvider.System`. Integration tests replace it with a `FakeTimeProvider`, which they set or advance through `factory.Clock`:

```csharp
factory.Clock.Advance(TimeSpan.FromDays(3));
```

`GET /api/v1/platform/clock-sample` is a sample endpoint that returns the time from this clock. Remove it when the first real endpoint reads time the same way.

### Email and external sign-in

The API never talks to an email provider or to Google or Microsoft directly from a feature. Integration tests replace both with fakes:

- **Email:** code sends mail through `IEmailSender` (`Modules/Platform/Email`). The test host registers `FakeEmailSender`, and every message the API sends lands in `factory.Email.Outbox`, oldest first. `factory.Email.Clear()` empties it.
- **External sign-in:** `factory.ExternalSignIn.SignIn(new FakeExternalAccount("Google", "subject", "ana@example.com"))` returns a ticket for that account. A request that sends the ticket in the `X-Fake-External-Ticket` header is authenticated as that account through the `FakeExternal` scheme.

The fakes live in `tests/Flatly.Api.IntegrationTests/Fakes`. The real Google and Microsoft sign-in flows and the email provider are added by their own issues, and they do not change these fakes.
