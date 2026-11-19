# Flatly Web

React, TypeScript and Vite responsive web app.

## Scripts

Run these from `web/` after `npm install`:

- `npm run dev`: start the Vite dev server.
- `npm run build`: type-check and build for production.
- `npm run typecheck`: type-check without emitting files.
- `npm run lint`: run ESLint.
- `npm run format`: check formatting with Prettier.
- `npm test`: run the Vitest suite once.

## API client

The typed client is generated from the API's OpenAPI document (`contracts/flatly-api.json`).

- `npm run api:generate`: downloads the document from a running API (`FLATLY_API_URL`, default `http://localhost:5080`, or pass the origin as an argument) and regenerates `src/api/schema.d.ts`. Start the API first (see `api/README.md`).
- `npm run api:types`: regenerates `src/api/schema.d.ts` from the committed contract, without a running API.
- `npm run api:check`: fails when the committed types do not match the contract. Run `api:types` and commit the result when the API changes.

Calls use `api` from `src/api/client.ts` with `unwrap()`, which throws an `ApiError`. Requests carry the session cookie. A 401 response runs the session-expired handler, which sends the visitor to login. TanStack Query does not retry client errors (4xx). Tests mock the API with MSW (`src/test/server.ts`); unmocked requests fail the test.
