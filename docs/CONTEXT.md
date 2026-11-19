# Glossary

The domain terms used in Flatly's code, specs, issues and UI. Use these words exactly, in English, in code and in GitHub text. The UI shows Spanish and English labels; the Spanish label is given for reference.

## Core terms

- **Flat** (piso): the shared home and the tenant of all data. Everything in the product belongs to one flat. See [ADR-002](adr/0002-per-flat-multi-tenancy.md).
- **Member** (miembro): a person with an account who can sign in. A member can belong to one or more flats.
- **Membership** (pertenencia): the link between a member and a flat. It records the member's role in that flat and when they joined or left. Access to flat data always goes through a membership.
- **Contact person** (punto de contacto): the member who speaks to the landlord for the flat, so that the landlord receives one message instead of several. Set in the landlord module.

## Living together

- **Chore** (tarea): a household task that repeats, such as cleaning the bathroom, taking out the bins or changing the sheets. Each chore has a weight, so that some chores count more than others.
- **Turn** (turno): one occurrence of a chore assigned to a member within a rotation. A turn is done when its member confirms it.

## Landlord and home

- **Issue** (incidencia): a maintenance problem in the flat that is reported to the landlord, such as a water leak or a broken lock. It has a category, an urgency, a status and a history. In GitHub, "issue" means a project issue; this term does not refer to that.
- **Home Pack** (ficha del piso): the flat's reference information. In MVP1 it holds the address, the landlord or administrator contact, insurance, emergency contacts and building rules. Later it also holds meter locations, manuals and documents.

## Usage notes

- Use the English term in code (`Flat`, `Member`, `Membership`, `Chore`, `Turn`, `Issue`, `HomePack`, `ContactPerson`).
- Do not use "household", "apartment" or "tenant" for the flat. "Tenant" means only the flat in the multi-tenancy sense.
- Do not use "task" for chores. "Task" means a unit of work in the delivery plan (for example, a sub-issue).
