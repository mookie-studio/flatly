# Conventions

How we work in `mookie-studio/flatly`: branches, commits, pull requests and language.

## Language

- All text on GitHub is in English: issues, pull requests, review comments, commit messages, branch descriptions and ADRs.
- Code identifiers, comments and documentation in the repository are in English.
- The UI ships in Spanish and English. Translations are part of the feature, not a separate task.

## Branches

- Branch from the latest `main`.
- Keep branches short-lived: one branch for one issue, merged within a few days.
- Name branches `<type>/<short-description>` using the commit types below, for example `feat/create-flat` or `docs/adr-002`.
- Rebase onto `main` when it moves, before opening or updating a pull request. Do not merge `main` into a feature branch.
- Delete the branch after its pull request is merged.

## Commits

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <short description in the imperative>

<optional body explaining why>

<optional footer, e.g. Closes #123>
```

- **Types:** `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `build`, `ci`.
- **Scopes:** `api`, `web`, `docs`, `ci` or the module name when the change is in one module (for example `chores`).
- Keep the description under 72 characters and in the imperative ("add", not "added").
- Use the body to explain why the change is needed when the diff does not make it obvious.

## Pull requests

- One pull request per issue. Link it with `Closes #<issue>` in the description.
- The pull request title follows the same format as a commit message.
- Fill in the [pull request template](../.github/PULL_REQUEST_TEMPLATE.md).
- A pull request is squash-merged into `main`, so the squashed commit message must follow the format above.
- Checks must pass before merging. Do not merge with failing checks.

## Issues

- Backend issues have 🧠 in the title. Frontend issues have ⚛ in the title. Issues that touch both carry both.
- Every issue has one MVP label: `mvp1`, `mvp2` or `post-mvp`.
- Use the domain words in [CONTEXT.md](CONTEXT.md).

## Architecture decisions

- Record each decision that is hard to reverse in an ADR, following [the template](adr/0000-template.md).
- ADRs are numbered and immutable once accepted. To change a decision, write a new ADR that supersedes the old one and update the old one's status line only.
