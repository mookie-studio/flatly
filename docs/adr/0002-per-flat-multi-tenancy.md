# ADR-002: Per-flat multi-tenancy

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

Flatly is used by people who share a flat. Almost all data in the product belongs to a flat, not to a single person: chores and turns, the shopping list, the calendar, house rules, issues with the landlord and the Home Pack. Members of one flat must see that data, and members of other flats must never see it.

The product will grow through several modules (Platform, Flats, Chores, Shopping, Calendar, Maintenance; see the API layout in [api/README.md](../../api/README.md)), so the tenancy rule has to be the same for every module and easy to apply to new entities. We also want one database and one App Service, as decided in [ADR-001](0001-stack-and-deployment.md), so the isolation has to work inside a single PostgreSQL database.

## Decision

- **The flat is the tenant.** Every entity that belongs to a flat carries a `FlatId`. A user never owns flat data directly; they reach it through a membership.
- **Access is granted by membership.** A request may read or change flat data only when the signed-in user has an active membership in that flat. Leaving a flat ends the membership and removes access, but keeps the history that the flat needs (for example, issues already reported).
- **Two layers enforce the boundary:**
  1. A global EF Core query filter on every flat-scoped entity, so queries return rows only for the flat in the current request scope.
  2. An explicit membership check in every endpoint that reads or writes flat data, returning "not found" for flats the user does not belong to, so that the existence of other flats is not revealed.
- **Each module owns its own `DbContext`** (see the vertical slice example in [api/README.md](../../api/README.md)). Each context applies the same filter and the same `FlatId` rule.
- **Tests must prove isolation.** Every flat-scoped endpoint has at least one test where a member of flat A asks for flat B's data and receives "not found".

## Consequences

- **One rule for every module:** a new module only has to add `FlatId` to its entities, apply the shared filter and call the shared membership check.
- **Risk of a missed filter:** a query written without the filter would leak data across flats. The cross-flat tests and code review are the safeguards, and any bypass of the query filter must be justified in review.
- **Shared database:** flats share tables, so indexes on `FlatId` are needed for the filtered queries to stay fast.
- **Migrations stay simple:** one schema serves all flats, so a migration applies to every flat at once. This fits the additive-migration rule in ADR-001.
- **Revisit if needed:** if a flat ever needs stronger isolation (for example, data residency), a new ADR must decide how to provide it. This ADR does not allow per-flat databases or schemas.

## Alternatives considered

- **A database or schema per flat:** rejected for MVP1. It multiplies migrations and connections across a single App Service and adds operational work that the product does not need yet.
- **PostgreSQL row-level security as the only guard:** not chosen as the sole mechanism. It depends on session settings being correct on every connection, so the application-level filter and membership check stay as the primary guard. Row-level security may be added later as a second layer through a new ADR.
- **Per-user data with sharing:** rejected. Chores, purchases and issues are shared records of the flat, and modelling them per user would duplicate them.
