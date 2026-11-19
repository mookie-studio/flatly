# ADR-004: Web styling, component library and theming

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

The web app needs a responsive shell that works on phones and desktops, with light and dark themes (#49). Before building screens we must choose how components are styled and where the design tokens live.

The spec for #49 suggested shadcn/ui as the default. `mookie-studio/entry-hub-frontend` already ships a React + Vite web app with Tailwind CSS v4, Base UI primitives and a token set defined in CSS. Both products are operated by the same team and should look and behave consistently.

Constraints:

- Keep the stack small and easy to maintain (ADR-001).
- Support light and dark themes without duplicating component styles.
- Keep accessible behaviour (focus, keyboard, ARIA) for dialogs, menus and similar controls.

## Decision

- **Styling:** Tailwind CSS v4 through `@tailwindcss/vite`, the same setup as `entry-hub-frontend`.
- **Tokens:** brand and neutral scales, and sticker colours (success, warning, error), are defined in CSS with `@theme`. Semantic tokens (`surface`, `ink`, `line`, `accent`, and so on) are defined on `:root` and redefined for dark mode under `:root[data-theme='dark']`. The media query `prefers-color-scheme` applies when no explicit theme is set.
- **Theme choice:** the user picks *System*, *Light* or *Dark*. The choice is stored in `localStorage` and read with fallbacks, because storage can be unavailable. The provider sets `data-theme` on `<html>`.
- **Component library:** we do not adopt a full component kit for now. The shell and simple components are written in-house with Tailwind. When a behaviour-heavy primitive is first needed (dialog, menu, select), we use Base UI (`@base-ui/react`), as `entry-hub-frontend` does, and record that in its own change.

## Consequences

- **Shared look:** both products read the same token names and the same visual language, so a brand change is made in the same way in each.
- **Ownership:** components live in this repository and are not copied from a kit, so there is no generated code to keep in sync.
- **More to build:** we write the simple components ourselves. Complex widgets wait for Base UI, so they are not hand-rolled.
- **Dark mode is tested per token:** any new colour must have a dark value on the semantic tokens, not only in a component.

## Alternatives considered

- **shadcn/ui (spec default):** copies components into the repo on top of Radix and Tailwind. It would give a second design system next to `entry-hub-frontend`, so it was not chosen.
- **MUI:** a large theming system with its own styling engine. It conflicts with the Tailwind setup in `entry-hub-frontend`.
- **Mantine:** a complete kit with its own theming API. The same objection applies.
