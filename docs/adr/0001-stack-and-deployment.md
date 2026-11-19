# ADR-001: Stack and deployment

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

Flatly is mookie-studio's platform for people who share a flat: members, invitations, chores and rotations, and a landlord channel for maintenance. Before implementing the API and the web app we need to fix the core technology choices, how the product is deployed, and the scope of the first release. These decisions were discussed and confirmed in #1.

Constraints:

- The team wants a single, simple deployment target and no Kubernetes or container orchestration for now.
- The API deployment follows the same pipeline as `entry-hub`, so both products are operated the same way.
- The product is used on phones and desktops, from the browser.
- The UI must be available in Spanish and English from the first release.
- MVP1 focuses on text-only functionality. Images and file uploads are deferred to MVP2 (#12).

## Decision

### Backend (API)

- **Framework:** .NET 10 Minimal API.
- **Data access:** EF Core with PostgreSQL.
- **Local development:** orchestrated with .NET Aspire, which starts the API and its PostgreSQL dependency.
- **Multi-tenancy:** the flat is the tenant. Every entity belongs to a `FlatId`, and authorization is based on flat membership.
- **Reminders:** scheduled jobs run inside the API, and notifications are sent by email. Web push is deferred.
- **Sign-in:** ASP.NET Core Identity with external providers. MVP1 supports Google and Microsoft accounts (#11) and has no email/password login. Apple sign-in is added in MVP2 (#32).
- **Hosting:** a single Azure App Service. We do not use Kubernetes, and we do not build container images for now (#14).
- **Deployment pipeline (as in `entry-hub`):**
  - A GitHub Actions workflow runs when a version tag (`v*`) is pushed, or by hand with a chosen tag. Hand runs are also how a failed deployment is redone.
  - The build job runs the API tests, then publishes the API as an artifact.
  - The deploy job runs in the `production` GitHub environment, which holds the protection rules and the secrets.
  - Migrations run first, using the API's `migrate` command with the database owner role. The API itself connects with an ordinary role that has no schema rights.
  - Azure login uses OIDC federated credentials, so no password is stored. The API is deployed to the App Service with `azure/webapps-deploy`.
  - A smoke test calls `/health` and `/api/v1/health`. If it fails, the workflow redeploys the previous tag.
  - Migrations are only additive, so the previous version keeps working on the new schema.
- **Staging:** deferred. Tracked in backlog #130.

### Frontend (web)

- **Stack:** React, TypeScript, Vite, React Router and TanStack Query.
- **API client:** a typed client generated from the API's OpenAPI document.
- **Layout:** responsive web. A PWA is tracked in the backlog (#33).
- **Hosting:** Cloudflare Pages, project `flatly-web`, deployed the same way as `entry-hub-frontend`.
- **Languages:** Spanish and English from day one (#10).

### Repository layout

Open. Monorepo (`api/`, `web/`, `docs/`) versus a separate frontend repository is not decided by this ADR.

### Scope

- **MVP1:** foundations, flats, members and invitations, chores and rotations, and the landlord module (text-only issues, history, landlord message, no-reply reminder, basic Home Pack).
- **MVP2:** images and uploads, shopping and inventory, calendar and house rules, document library, and Apple sign-in.

## Consequences

- **Simple operations:** one App Service for the API keeps deployment and cost low. Scaling beyond a single instance will need a revisit.
- **Shared model:** using the flat as the tenant means every query and authorization check must filter by `FlatId`. This must be enforced in the API and covered by tests.
- **Shared pipeline:** keeping the `entry-hub` pipeline shape means fixes to deployment tooling can be reused across both products.
- **Migrations discipline:** because a rollback does not undo migrations, every migration must be additive.
- **No staging environment yet:** releases go straight to production until staging is added (#130).
- **Local parity:** Aspire gives a repeatable local environment with PostgreSQL.
- **Typed contract:** generating the client from OpenAPI keeps the frontend in sync with the API, but the API must keep its OpenAPI document accurate.
- **Identity with external providers:** no password storage or email verification flows in MVP1. Users without a Google or Microsoft account cannot sign in until another provider is added.
- **Uploads are deferred:** MVP1 has no file storage. The storage provider for MVP2 is still open (Azure Blob is the likely fit).

## Alternatives considered

- **Kubernetes or container-based hosting:** rejected for now, because it adds operational overhead the team does not need for MVP1.
- **Email/password login in MVP1:** rejected. Google and Microsoft sign-in covers the target users with less code to maintain.
