# ADR-003: Identity with external providers

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

Flatly needs accounts so that members can join a flat, see its data and act on it. The people who use it are not technical, often share one flat with people they have just met, and mostly sign in from a phone. Storing passwords and running email verification and password reset flows would add a large amount of security-sensitive code, and it would also be one more password for users to manage.

Most target users already have a Google or Microsoft account. Apple accounts are wanted too, but they are not needed in MVP1.

The API uses ASP.NET Core Identity, as decided in [ADR-001](0001-stack-and-deployment.md). The web app is a single-page application served from Cloudflare Pages and talks to the API on Azure App Service.

## Decision

- **Identity provider:** ASP.NET Core Identity stores the user accounts and the links to external providers.
- **MVP1 providers:** Google and Microsoft accounts. Users sign in only through these providers.
- **No email/password login in MVP1.** The product does not store passwords, and it does not send sign-in or verification emails for accounts. Notification emails (for reminders) are a separate concern.
- **MVP2 provider:** Apple sign-in is added in MVP2 (#32). Adding it is a new provider link, not a new login model.
- **Session:** the web app uses a cookie session issued by the API after the external sign-in completes. The web app does not store provider tokens.
- **Changing this decision** (for example, adding email login) requires a new ADR that supersedes this one.

## Consequences

- **Less security-sensitive code:** no password hashing, reset tokens or email verification in MVP1.
- **Users without a Google or Microsoft account cannot sign in** in MVP1. This is accepted for the MVP. The target users are expected to have one of the two.
- **Provider dependency:** if Google or Microsoft changes its sign-in terms or has an outage, Flatly cannot sign people in. The product must show a clear message when a provider fails.
- **Account linking:** a person who signs in with both Google and Microsoft using the same email address needs an explicit rule. The rule belongs to the Platform module and must be recorded in its spec before implementation.
- **Cookie sessions and CORS:** the cookie setup must work with the web origin on Cloudflare Pages and the API origin on Azure App Service. Any change to these origins is a deployment change to review against this ADR.
- **Apple later:** Apple sign-in in MVP2 must not change the MVP1 flows. The ADR will be revisited only if Apple requires a different model.

## Alternatives considered

- **Email and password with verification:** rejected for MVP1. It adds code and support work, and it does not match the target users' habits.
- **Magic links by email:** rejected for MVP1 for the same reason. It still depends on email delivery for sign-in, which we want to avoid for the first release.
- **A hosted identity service for all sign-in:** not chosen. It would add a new vendor and a second identity store next to ASP.NET Core Identity, without a clear gain for the two providers in scope.
