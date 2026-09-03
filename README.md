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
│   ├── frontend/   Angular workspace: Agent CRM + Customer Portal (CRM-104)
│   └── backend/    ASP.NET Core solution (populated by CRM-105)
├── tests/          Cross-cutting / integration tests (populated by CRM-202)
├── scripts/        Automation entry points: bootstrap, dev, test, migrate, reset (CRM-203)
├── env/            `*.env.example` templates; local `*.env` files are git-ignored
├── docs/           ADRs, architecture and development process docs
├── .squad/         Squad Kit story intake and generated plans (tool-managed)
└── .claude/        Claude Code configuration (tool-managed)
```

`src/backend/` currently holds only a placeholder --- do not add code there
under CRM-107. `src/frontend/` is populated by CRM-104; see
[`src/frontend/README.md`](src/frontend/README.md).

## Prerequisites

-   **Git**.
-   **Node.js >= 22.12** --- pinned by CRM-104 (developed against v22.21.1).
-   **.NET SDK** --- exact version pinned by CRM-105.
-   **Docker Engine + Compose v2 or newer** --- **required** to run the
    local infrastructure (`docker compose up -d`). Developed against
    Docker 29.7.2 and Docker Compose v5.5.0. Docker Desktop or any
    compatible engine works.

Only Git is required to work on this story; the remaining versions are
pinned by the sibling Sprint 0 stories that introduce them.

## First-time setup (fresh clone)

1.  `git clone <repository-url> && cd crm`
2.  `cp env/backend.env.example env/backend.env`
3.  `cp env/frontend.env.example env/frontend.env`
4.  Edit both files with local values. They are git-ignored and must never
    be committed.
5.  Install the frontend workspace: `cd src/frontend && npm ci`.
6.  Start the complete local stack from the repository root:
    `docker compose up --build` (see
    [Local infrastructure](#local-infrastructure-docker-compose)). To start
    only PostgreSQL, use
    `export COMPOSE_ENV_FILES=env/backend.env && docker compose up -d postgres`
    instead.
7.  .NET restore/build (CRM-105) becomes available when that story lands;
    this README is updated at that time.

### Angular frontend

The Angular workspace lives at `src/frontend/` and hosts both the **Agent
CRM** and **Customer Portal** applications plus the shared
`@squad-crm/*` libraries. Smoke check from a fresh clone:

```bash
cd src/frontend
npm ci && npm run build
```

Then `npm run start:agent-crm` (port 4200) or `npm run start:customer-portal`
(port 4300). Workspace layout, dependency boundaries, the runtime
configuration contract and the localization/direction foundation are
documented in [`src/frontend/README.md`](src/frontend/README.md).

## Local infrastructure (Docker Compose)

`docker-compose.yml` at the repository root now runs the **complete local
Squad CRM stack**: **PostgreSQL**, the **backend API**, **Agent CRM** and
**Customer Portal**. PostgreSQL is pinned to the concrete, verified image
tag `postgres:18.6-alpine3.24` --- an `-alpine` variant for image size, and
a fixed patch/base tag so two developers pulling on different days get the
same server. No locally installed PostgreSQL server is required, and no
locally installed .NET SDK or Node.js is required either if you only use
Docker.

### Full stack

The `backend` service loads **every** value from `env/backend.env`
directly, so that file must exist before `docker compose up --build` is
run — this is now a hard prerequisite, not just a developer-safe fallback.

```bash
# 1. Prepare local environment values (once per clone) — required.
cp env/backend.env.example env/backend.env

# 2. Build and start the complete stack (from the repository root)
docker compose up --build

# 3. Open:
#    - Agent CRM:          http://localhost:4200
#    - Customer Portal:    http://localhost:4300
#    - Backend API health: http://localhost:5080/health/ready

# 4. Stop, preserving all data
docker compose down
```

Stack-wide reset uses the same destructive `docker compose down -v`
documented below; it also removes the built application containers, not
just PostgreSQL.

After the **first** `docker compose up --build` against a fresh
`squadcrm-pgdata` volume, apply module migrations from the host once ---
`dotnet ef database update --project … --context …` per
[`src/backend/README.md`](src/backend/README.md) --- before exercising
data-backed features. The `backend` service does not run migrations
automatically; it stays consistent with the explicit, non-destructive
migration workflow described there.

### PostgreSQL only

To start just the database (for example, while running the backend or
Angular apps directly with `dotnet run` / `npm run start:agent-crm`),
Compose still reads values from `env/backend.env` (git-ignored). Every
PostgreSQL variable also has a developer-safe default, so a fresh clone
starts without that file --- but step 1 below is still the documented
first step, because the backend and `dotnet ef` read these values from the
process environment.

```bash
# 1. Prepare local environment values (once per clone)
cp env/backend.env.example env/backend.env

# 2. Start PostgreSQL only (from the repository root)
export COMPOSE_ENV_FILES=env/backend.env   # PowerShell: $env:COMPOSE_ENV_FILES="env/backend.env"
docker compose up -d postgres

# 3. Check status and health
docker compose ps
docker compose logs -f postgres

# 4. Stop, preserving all data
docker compose down
```

Instead of the environment variable you may pass the env file per command:
`docker compose --env-file env/backend.env up -d postgres`. **Compose v2 or
newer is required** --- `COMPOSE_ENV_FILES` and the top-level `name:` key do
not exist in the hyphenated Compose v1.

```bash
# DESTRUCTIVE — deletes the squadcrm-pgdata volume and every local database row.
# There is no undo. Only use this to start from a clean database.
docker compose down -v
```

### Configuration contract

| Compose / env variable | Meaning | ASP.NET Core configuration it feeds |
|---|---|---|
| `POSTGRES_HOST` | Host-side hostname; always `localhost` for host-run apps | `Host=` in the Npgsql connection string |
| `POSTGRES_PORT` | Published host port; container always listens on `5432` | `Port=` |
| `POSTGRES_DB` | Database created on first start | `Database=` |
| `POSTGRES_USER` | Superuser created on first start | `Username=` |
| `POSTGRES_PASSWORD` | Local developer password only | `Password=` |

These five keys are the **only** database configuration an operator ever
sets. The backend derives one Npgsql connection string from them **once at
composition time** and publishes it internally as
`ConnectionStrings:SquadCrmPostgres`; that name is application-internal and
is never read from a file or the environment. Missing or invalid values fail
fast at startup with a message naming the offending keys --- never their
values, and never the password or the assembled connection string. The same
single implementation serves `dotnet ef`, so design time and runtime cannot
disagree. See [`src/backend/README.md`](src/backend/README.md) (**Persistence**)
for the schema-per-module details. **No production credential is required or
accepted in `env/backend.env`;** the committed `env/backend.env.example` holds
developer-safe local defaults only.

### Operational notes

-   **Port 5432 already in use** (a locally installed PostgreSQL, or another
    project): set `POSTGRES_PORT` in `env/backend.env` to a free port, e.g.
    `POSTGRES_PORT=55432`, and run `docker compose up -d` again. The container
    still listens on `5432` internally; only the published host port moves.
-   **Loopback only.** The port is published on `127.0.0.1`, so the database is
    reachable from applications on this machine but not from the local network.
    Verify with `docker compose port postgres 5432`.
-   **Data lives in the named volume `squadcrm-pgdata`,** mounted at
    `/var/lib/postgresql` (PostgreSQL 18+ images require the mount at that
    level and store data in a major-version subdirectory below it).
    `docker compose down` preserves the volume; only `docker compose down -v`
    destroys it.
-   **Changing credentials after the first start has no effect.** PostgreSQL
    applies `POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_DB` only when it
    initialises an empty data directory. To change them you must run the
    destructive `docker compose down -v` and then `docker compose up -d`.
-   **No restart policy is configured.** After a Docker Engine or machine
    restart, PostgreSQL does not come back by itself --- run
    `docker compose up -d` again. The named volume means no data is lost.
-   **Health.** The service reports `healthy` once
    `pg_isready -U <user> -d <db>` succeeds inside the container. First start
    runs `initdb`, which the 30s `start_period` absorbs. If the service stays
    unhealthy, `docker compose logs postgres` is the diagnostic.
-   Commands address the Compose **service** name (`postgres`), never a
    container name --- no `container_name:` is set, so Compose generates names
    that cannot collide across clones or git worktrees.

## Common commands

| Command | What it does | Story that adds it |
|---------|--------------|--------------------|
| `cp env/backend.env.example env/backend.env` | Create the local backend env file | available today |
| `cp env/frontend.env.example env/frontend.env` | Create the local frontend env file | available today |
| `git check-ignore -v env/backend.env` | Confirm local env files are ignored | available today |
| `cd src/frontend && npm ci` | Install the Angular workspace dependencies | available today |
| `cd src/frontend && npm run start:agent-crm` | Run the Agent CRM dev server (port 4200) | available today |
| `cd src/frontend && npm run start:customer-portal` | Run the Customer Portal dev server (port 4300) | available today |
| `cd src/frontend && npm run build` | Production build of both frontend applications | available today |
| `cd src/frontend && npm run lint` | Lint the workspace, including dependency boundaries | available today |
| Backend restore / run | Restore and run the modular monolith | CRM-105 |
| `cd src/backend && dotnet tool restore` | Restore the pinned `dotnet-ef` local tool | available today |
| `cd src/backend && dotnet ef database update --project … --context …` | Apply one module's migrations | available today |
| `docker compose up --build` | Build and start the full local stack (PostgreSQL + backend + both Angular apps) | available today |
| `docker compose up -d postgres` | Start only the local PostgreSQL infrastructure | available today |
| `docker compose ps` | Show stack status and health | available today |
| `docker compose down` | Stop the stack, preserving all data | available today |
| `docker compose down -v` | **DESTRUCTIVE** --- stop and delete the `squadcrm-pgdata` volume | available today |
| `scripts/migrate` | Apply every current module migration | available today |
| `scripts/seed` | Idempotently add synthetic development/test fixture data | available today |
| StaffIdentity bootstrap command below | Explicitly create or reset one local Development staff account | CRM-110 |
| `scripts/reset --yes` | **DESTRUCTIVE:** recreate local PostgreSQL, migrate and seed | available today |

## Migrations and tests

EF Core migrations are live. Each module owns its own schema and its own
migrations; there is no shared `DbContext` and nothing is applied at
application startup. Automated test orchestration in CI remains CRM-202's.

```bash
# 1. Infrastructure up (from the repository root)
export COMPOSE_ENV_FILES=env/backend.env
docker compose up -d

# 2. Load the operator values into the shell (from src/backend/)
cd src/backend
set -a && . ../../env/backend.env && set +a

# 3. Restore the pinned tooling and apply the module's migrations
dotnet tool restore
dotnet ef database update \
  --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --context ArchitectureFixtureDbContext

# 4. Verify what was created
docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
  -c '\dt architecture_fixture.*'
```

```bash
# DESTRUCTIVE — deletes the squadcrm-pgdata volume and every local database row.
# There is no undo. Run it to prove the database recreates from migrations alone,
# then repeat steps 1-4 above.
docker compose down -v
```

The repeatable CRM-203 workflow wraps those steps:

```bash
# Apply current module migrations, then idempotent synthetic fixture data.
scripts/migrate
scripts/seed

# DESTRUCTIVE: delete only the Compose-managed local PostgreSQL volume,
# recreate it, apply migrations and add the synthetic fixture row.
scripts/reset --yes
```

`scripts/reset` refuses to run without `--yes`. The seed contains no customer,
credential or production-derived data and is never run at application startup.
The reset targets `POSTGRES_VOLUME_NAME` (default `squadcrm-pgdata`); an isolated
verification stack can supply a separate env file through
`SQUADCRM_BACKEND_ENV_FILE` without touching normal local data.

### Create or reset a local staff account

After PostgreSQL is running and `scripts/migrate` has completed, run this
explicit Development-only command from the repository root:

```bash
set -a && source env/backend.env && set +a
dotnet run \
  --project src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap \
  -- agent@example.test
```

The command prompts twice for the password without echoing it. It creates an
active staff user when the normalized email is new; otherwise it resets that
user's password, reactivates the account and revokes existing refresh sessions.
It refuses to run unless `ASPNETCORE_ENVIRONMENT` is exactly `Development` and
is never called by application startup, `scripts/seed` or `scripts/reset`.

For local automation only, pass the password in the command process environment
and remove it immediately afterward. Never add it to `env/backend.env`, another
configuration file, shell history or source control:

```bash
read -r -s -p "Password: " SQUADCRM_BOOTSTRAP_STAFF_PASSWORD && echo
export SQUADCRM_BOOTSTRAP_STAFF_PASSWORD
dotnet run \
  --project src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap \
  -- agent@example.test
unset SQUADCRM_BOOTSTRAP_STAFF_PASSWORD
```

Every `dotnet ef` command needs those `POSTGRES_*` values in the process
environment: the application never reads `env/backend.env` itself. Without
them the command fails fast naming the missing keys and printing no value.

### Bootstrap the first role administrator

This is privileged operator tooling, not a user-management workflow. First create an
active staff subject and an active global role through the normal CRM-110/CRM-112
paths. Then, with the `POSTGRES_*` environment loaded, explicitly assign that subject
to the role and grant the minimum `roles.view` and `roles.manage` capabilities:

```bash
dotnet run \
  --project src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap \
  -- --subject-email agent@example.test --role-code ADMINISTRATOR
```

The command is safe to repeat. It rejects missing/inactive subjects or roles, accepts
no credential, creates no default administrator, and is never run by API startup,
seed, migration, or reset scripts. Treat access to this command and its database
configuration as privileged production operator access.

The full backend test run **requires** the database to be up --- the
persistence suite creates and removes an isolated `squadcrm_tests_*` database
and fails, rather than skipping, without PostgreSQL. It never resets the
configured development database. The
architecture and API suites run with no database:
`cd src/backend && dotnet test tests/SquadCrm.ArchitectureTests` and
`dotnet test tests/SquadCrm.Api.Tests`.

The frontend unit/component baseline runs with
`npm test --prefix src/frontend -- --no-progress`. CRM-202's minimal
`.github/workflows/tests.yml` executes these representative backend and
frontend suites in CI. Comprehensive CI quality gates and seed/reset tooling
remain CRM-203 scope.

The completed quality-gate and emergency-bypass policy is documented in
[`docs/development/ci-quality-gates.md`](docs/development/ci-quality-gates.md).

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
