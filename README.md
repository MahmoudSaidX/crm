# Squad CRM --- Final Implementation Package

Repository-side context for Claude Code + Squad Kit + Superpowers. This
repository hosts the Squad CRM Angular frontend, the ASP.NET Core modular
monolith backend and the shared docs that govern both. Sprint 0 establishes
the workspace baseline; feature modules follow once the workspace, solution
and infrastructure stories land.

## Source of truth

1.  Linear: stories, acceptance criteria, business rules, dependencies
    and delivery state.
2.  `.squad/`: active story intake and generated plan.
3.  `docs/adr/`: architecture constraints.
4.  Codebase: implementation reality.

## Stack

-   Angular + TypeScript + **PrimeNG** + PrimeIcons.
-   ASP.NET Core modular monolith.
-   PostgreSQL + EF Core, schema-per-module.
-   Docker Compose, Hangfire, transactional outbox, OpenTelemetry.
-   Provider-neutral adapters; Ollama for free local AI.

Start with CRM-107. Then CRM-104 and CRM-105 can proceed in parallel
subject to Linear blockers.

## Repository layout

```
.
├── src/
│   ├── frontend/   Angular workspace (populated by CRM-104)
│   └── backend/    ASP.NET Core solution (populated by CRM-105)
├── tests/          Cross-cutting / integration tests (populated by CRM-202)
├── scripts/        Automation entry points: bootstrap, dev, test, migrate, reset (CRM-203)
├── env/            `*.env.example` templates; local `*.env` files are git-ignored
├── docs/           ADRs, architecture and development process docs
├── .squad/         Squad Kit story intake and generated plans (tool-managed)
└── .claude/        Claude Code configuration (tool-managed)
```

`src/frontend/` and `src/backend/` currently hold only placeholders --- do
not add code there under CRM-107.

## Prerequisites

-   **Git**.
-   **Node.js LTS** --- exact version pinned by CRM-104.
-   **.NET SDK** --- exact version pinned by CRM-105.
-   **Docker Desktop** (or a compatible engine) --- used by CRM-197.

Only Git is required to work on this story; the remaining versions are
pinned by the sibling Sprint 0 stories that introduce them.

## First-time setup (fresh clone)

1.  `git clone <repository-url> && cd crm`
2.  `cp env/backend.env.example env/backend.env`
3.  `cp env/frontend.env.example env/frontend.env`
4.  Edit both files with local values. They are git-ignored and must never
    be committed.
5.  Frontend dependency install (CRM-104), .NET restore/build (CRM-105) and
    the Docker Compose infrastructure (CRM-197) become available when those
    stories land; this README is updated at that time.

## Common commands

| Command | What it does | Story that adds it |
|---------|--------------|--------------------|
| `cp env/backend.env.example env/backend.env` | Create the local backend env file | available today |
| `cp env/frontend.env.example env/frontend.env` | Create the local frontend env file | available today |
| `git check-ignore -v env/backend.env` | Confirm local env files are ignored | available today |
| Frontend install / serve | Install deps and run the Angular dev server | CRM-104 |
| Backend restore / run | Restore and run the modular monolith | CRM-105 |
| Infrastructure up / down | Start PostgreSQL and friends via Docker Compose | CRM-197 |
| `scripts/bootstrap`, `scripts/dev`, `scripts/test`, `scripts/migrate`, `scripts/reset` | Automation entry points | CRM-203 |

Rows marked with a story id are previews --- nothing in the repository
provides them yet.

## Migrations and tests

EF Core migrations arrive with CRM-106 and the automated test scaffolding
with CRM-202. There is no runtime code, database schema or test runner in
the repository yet. Both stories update this section when they land, per the
docs update rule below.

## Contributing

-   [Branching and commit conventions](docs/development/branching-and-commits.md)
-   [Definition of Ready](docs/development/definition-of-ready.md)
-   [Definition of Done](docs/development/definition-of-done.md)
-   [Implementation workflow](docs/development/implementation-workflow.md)

**Docs update rule:** any change to setup commands or configuration
requires an update to this `README.md` in the same pull request, and when
the change is architectural, an accompanying ADR under `docs/adr/`.

## Architecture

-   [Architecture overview](docs/architecture/architecture.md)
-   [Frontend architecture](docs/architecture/frontend.md)
-   [Data ownership](docs/architecture/data-ownership.md)
-   [Architecture Decision Records](docs/adr/) --- ADR-001 modular monolith
    through ADR-011 testing.

## Squad Kit / Claude Code

`.squad/` and `.claude/` are managed by tooling. Do not hand-edit them;
change them only through the documented Squad Kit and Claude Code flows.
The `# Managed by squad-kit` block at the top of `.gitignore` is likewise
tool-owned --- append repository ignore rules below it.
