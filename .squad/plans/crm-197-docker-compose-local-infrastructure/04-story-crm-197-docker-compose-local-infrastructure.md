# Story 04 — Docker Compose & Local Infrastructure (Story: CRM-197)

## Prerequisites

- **Story 01 completed (CRM-107 — Repository & Developer Workflow):** repository baseline, `env/backend.env.example`, root `README.md` and the `.gitignore` block `# Docker / local infra (CRM-197)` (`.gitignore:52-54`, already ignoring `.docker/` and `docker-compose.override.yml`) are in place. See [`../repository-and-developer-workflow/01-story-repository-developer-workflow.md`](../repository-and-developer-workflow/01-story-repository-developer-workflow.md).
- **Story 03 completed (CRM-105 — ASP.NET Core Modular Monolith Foundation):** the backend configuration contract this story must stay compatible with. See [`../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md`](../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md). Follow its "inspect, do not assume" version-pinning pattern — it applies here to the PostgreSQL image tag.
- **`docs/adr/ADR-002-postgresql.md` is a binding read-only input** — PostgreSQL + EF Core with schema-per-module. This story provides **only the PostgreSQL server**; EF Core, schemas and migrations are CRM-106. Do not amend the ADR.
- **Docker Engine + Compose v2 or newer** available on the developer machine. Observed at planning time: `Docker version 29.7.2` / `Docker Compose version v5.5.0`.
- Coordinate with the owners of downstream stories that consume these coordinates: CRM-106 (persistence), CRM-199 (Hangfire), CRM-201 (observability), CRM-202 (testing). **Do not implement any of them here.**

---

## Story Goal

Give every developer a reproducible, environment-driven local PostgreSQL instance started with plain `docker compose` commands, so that Squad CRM dependencies no longer vary by machine and no production credential is ever required locally.

1. `docker compose up -d` from the repository root starts PostgreSQL and reaches a **healthy** state.
2. The database name, user, password and published host port are **environment-configurable** and read from the **existing** `env/backend.env` contract — no competing configuration file is introduced.
3. Data lives in a **named Docker volume**, so `docker compose down` preserves it and only an explicit, documented destructive command removes it.
4. Root `README.md` documents copy-pasteable commands for prepare / start / status / stop / reset.

**Explicitly out of scope:** EF Core, migrations, business schemas/tables/init SQL, application persistence code (CRM-106); outbox (CRM-198); Hangfire (CRM-199); file storage (CRM-200); OpenTelemetry / readiness probes (CRM-201); the testing foundation (CRM-202); Angular or ASP.NET Core application containers; email/WhatsApp/SMS emulators; Kubernetes, cloud or production deployment infrastructure; production credentials.

---

## Context — Read These Files First

1. `.squad/stories/crm-197-docker-compose-local-infrastructure/crm-197-docker-compose-local-infrastructure/intake.md` — the full intake. **PostgreSQL**, **Environment configuration**, **Networking**, **Persistence/reset behavior**, **Health**, **Developer workflow** and **Out of scope** are hard constraints.
2. `env/backend.env.example` — **~lines 8–13**. The `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` keys **already exist** and are commented `# PostgreSQL (populated by CRM-106 / CRM-197)`. **Reuse these exact names.** Do not rename them and do not create a second env contract.
3. `.gitignore` — **~lines 34–54**. Confirm `*.env` is ignored with `!*.env.example` re-included (~lines 41–43), and that the CRM-197 block already ignores `.docker/` and `docker-compose.override.yml` (~lines 52–54).
4. `README.md` — **~lines 47–56** (Prerequisites, "Docker Desktop … used by CRM-197"), **~lines 57–68** (First-time setup, step 6 promises the Compose infrastructure), **~lines 85–102** (Common commands table; row `| Infrastructure up / down | … | CRM-197 |` at **line 98**), **~lines 118–120** (docs update rule). These are the four places this story must update.
5. `src/backend/src/Api/SquadCrm.Api/appsettings.json` and `appsettings.Development.json` — **read-only**. Note there is **no `ConnectionStrings` section**; CRM-197 must not add one. Confirms the backend currently opens no database connection.
6. `src/backend/src/Api/SquadCrm.Api/Program.cs` — **~lines 54–55**, `builder.Services.AddHealthChecks()` is liveness-only with the comment "No database/storage/provider probes (owned by later stories)". **Do not touch this file.**
7. `docs/adr/ADR-002-postgresql.md` — the persistence baseline. Read-only input.
8. `src/backend/README.md` — **~lines 25–47** for the commands/URL table style to mirror in the README additions.
9. `CLAUDE.md` — Architecture and Security/quality constraints; "Never log passwords, tokens, OTPs or provider secrets."

Grep hints while implementing:

- `grep -rn "CRM-197" README.md .gitignore` — the four existing placeholders that must be resolved (`README.md:52`, `README.md:65-66`, `README.md:98`, `.gitignore:52`).
- `grep -n "POSTGRES" env/backend.env.example` — the exact variable names to reuse.
- `grep -rn "ConnectionStrings\|Npgsql\|EntityFrameworkCore" src/backend/src` — must stay empty after this story.

---

## Product rules (from story)

**Current behaviour:** there is no Compose file anywhere in the repository. `env/backend.env.example` declares `POSTGRES_*` keys that nothing consumes. `README.md` advertises Docker Compose infrastructure as a CRM-197 preview that "nothing in the repository provides yet" (`README.md:101-102`). Running the backend requires no database.

**New behaviour:**

- `docker compose config` succeeds from the repository root and resolves every variable.
- `docker compose up -d` starts one service, **`postgres`**, which reports `healthy` in `docker compose ps`.
- PostgreSQL is reachable from the host at `localhost:${POSTGRES_PORT}` using `${POSTGRES_USER}` / `${POSTGRES_DB}` from `env/backend.env`.
- The published port is bound to the **loopback interface only** — the database is not reachable from the local network.
- `docker compose down` stops the container and **preserves** the named volume; data written before the stop is present after the next `up`.
- `docker compose down -v` is the **only** documented way to destroy local data, and is documented as destructive.
- Nothing in the repository requires, contains or requests a production credential.

---

## Implementation tasks

### 1 — Pin the PostgreSQL image (verify the tag, do not derive it)

**Do not use `postgres:latest`. Do not invent a tag.** The Compose file must reference a **concrete PostgreSQL image tag that has been verified to exist and pull successfully**. Never derive a tag from the `PG_VERSION` environment variable inside another image — `PG_VERSION` describes the server build, not a published Docker Hub tag, and a tag constructed from it may not exist.

**Step 1 — inspect the available/supported tags.** List what Docker Hub actually publishes for a currently supported PostgreSQL major, for example:

```bash
docker run --rm curlimages/curl -s \
  'https://hub.docker.com/v2/repositories/library/postgres/tags?page_size=100&name=alpine' \
  | tr ',' '\n' | grep '"name"'
```

(Any equivalent means of listing published tags is acceptable — Docker Hub UI, `skopeo list-tags docker://docker.io/library/postgres`, `crane ls postgres`.)

**Step 2 — make the pinning decision explicit.** Choose one **concrete, non-floating** tag from a currently supported PostgreSQL major, preferring an `-alpine` variant for image size. Write the chosen tag and the reason for choosing it into the pull request description and the `README.md` note.

**Step 3 — verify it pulls.** Run:

```bash
docker pull postgres:<chosen-tag>
docker image inspect postgres:<chosen-tag> --format '{{.Id}} {{index .RepoTags 0}}'
```

Both must succeed. **Use exactly the verified tag string in `docker-compose.yml`** — no rewriting, no reconstruction from image metadata.

**Prohibited:** `postgres:latest`; a fabricated tag such as `postgres:17.x-alpine`; any floating tag adopted without first making the pinning decision explicit per Step 2.

**Not required:** digest pinning (`postgres@sha256:…`) is explicitly out of scope for CRM-197.

**Rationale to record in `README.md`:** the `-alpine` variant is chosen for image size; the tag is concrete and verified so two developers pulling on different days get the same server.

### 2 — Create the Compose file

**Create file: `docker-compose.yml`** (repository root).

- **Do not** add a top-level `version:` key — it is obsolete in Compose v2+ and emits a warning under `docker compose config`.
- Every credential-bearing value comes from **variable substitution with a developer-safe default**, so a developer who has not yet copied `env/backend.env` still gets a working local stack and **no secret is baked into the file**.

```yaml
name: squadcrm

services:
  postgres:
    # Concrete tag verified by `docker pull` in Task 1. Never `latest`, never fabricated.
    image: postgres:<verified-tag-from-task-1>
    environment:
      POSTGRES_DB: ${POSTGRES_DB:-squadcrm}
      POSTGRES_USER: ${POSTGRES_USER:-squadcrm}
      POSTGRES_PASSWORD: ${POSTGRES_PASSWORD:-change-me}
    ports:
      # Loopback-only: reachable from host-run apps, not from the local network.
      - "127.0.0.1:${POSTGRES_PORT:-5432}:5432"
    volumes:
      - squadcrm-pgdata:/var/lib/postgresql/data
    healthcheck:
      # Meaningful check: the configured user can reach the configured database.
      test: ["CMD-SHELL", "pg_isready -U \"$${POSTGRES_USER}\" -d \"$${POSTGRES_DB}\" || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 30s

volumes:
  squadcrm-pgdata:
    name: squadcrm-pgdata
```

Notes the executor must honour:

- `$${...}` inside `healthcheck.test` is **intentional** — the doubled `$` escapes Compose substitution so the variable is expanded **inside the container** from the values already set in `environment:`. Using a single `$` would bake the resolved user/database into the Compose file at parse time.
- The named volume is declared with an explicit `name:` so it is stable regardless of the project name.
- **No `container_name:`.** Compose derives the container name from the project name; hard-coding one collides across clones and git worktrees. Every documented command addresses the **service** name (`docker compose exec postgres …`, `docker compose logs postgres`, `docker compose ps`), never a container name.
- **No `restart:` policy.** The local infrastructure lifecycle stays explicit — `docker compose up -d` starts it, `docker compose down` stops it. PostgreSQL must not come back up merely because Docker Desktop/Engine restarted.
- **No `command:`, no `init.sql`, no `docker-entrypoint-initdb.d` mount.** Creating schemas or tables is CRM-106's job.
- **Only this one service.** Do not add pgAdmin, Redis, MinIO, Mailhog, Ollama or any other future dependency.

### 3 — Wire the existing environment contract

**File: `env/backend.env.example`** — the five `POSTGRES_*` keys at **~lines 8–13** already exist and are already correct for these Compose coordinates (`POSTGRES_HOST=localhost`, `POSTGRES_PORT=5432`, `POSTGRES_DB=squadcrm`, `POSTGRES_USER=squadcrm`, `POSTGRES_PASSWORD=change-me`).

**Do not rename or re-value them.** Make exactly one edit: replace the section comment on **line 8** so it stops deferring the keys and instead states the contract, for example:

```
# PostgreSQL — consumed by docker-compose.yml (CRM-197).
# POSTGRES_HOST/POSTGRES_PORT are the *host-side* coordinates used by apps
# running directly on the developer machine. The container always listens on
# 5432 internally; POSTGRES_PORT only changes the published host port.
# Developer-safe local defaults. Never put a production credential here.
```

**Do not add** `ConnectionStrings__*`, `DATABASE_URL` or any other key. CRM-106 owns whatever the backend eventually binds; this story only guarantees the coordinates are compatible. Record the intended mapping in the README (Task 4) as documentation, not as configuration.

**How Compose reads these values.** Compose substitutes from the project directory's `.env` by default, which this repository does not use. The documented invocation is therefore:

```bash
export COMPOSE_ENV_FILES=env/backend.env    # or pass --env-file env/backend.env
docker compose up -d
```

Both forms must be documented. Because every variable in `docker-compose.yml` has a default, plain `docker compose up -d` with no env file also works and yields the developer-safe defaults — this is intentional, not an oversight.

### 4 — Update the root README

**File: `README.md`** — four edits, no restructuring.

1. **Prerequisites (~line 52)** — change "**Docker Desktop** (or a compatible engine) --- used by CRM-197." to state that Docker is now **required** to run local infrastructure, and note the versions this story was developed against (`Docker 29.7.2`, `Docker Compose v5.5.0`).
2. **First-time setup (~lines 65–67)** — replace the CRM-197 promise in step 6 with the real start command, keeping the CRM-105 restore/build reference intact.
3. **New section `## Local infrastructure (Docker Compose)`**, placed after `### Angular frontend` (~line 83) and before `## Common commands`. Mirror the table style of `src/backend/README.md:29-34`. It must contain, as copy-pasteable blocks:

   ```bash
   # 1. Prepare local environment values (once per clone)
   cp env/backend.env.example env/backend.env

   # 2. Start infrastructure (from the repository root)
   export COMPOSE_ENV_FILES=env/backend.env   # PowerShell: $env:COMPOSE_ENV_FILES="env/backend.env"
   docker compose up -d

   # 3. Check status and health
   docker compose ps
   docker compose logs -f postgres

   # 4. Stop, preserving all data
   docker compose down
   ```

   ```bash
   # DESTRUCTIVE — deletes the squadcrm-pgdata volume and every local database row.
   # There is no undo. Only use this to start from a clean database.
   docker compose down -v
   ```

   Also document, in a short table:

   | Compose / env variable | Meaning | ASP.NET Core configuration it will feed (CRM-106) |
   |---|---|---|
   | `POSTGRES_HOST` | Host-side hostname; always `localhost` for host-run apps | `Host=` in the Npgsql connection string |
   | `POSTGRES_PORT` | Published host port; container always listens on `5432` | `Port=` |
   | `POSTGRES_DB` | Database created on first start | `Database=` |
   | `POSTGRES_USER` | Superuser created on first start | `Username=` |
   | `POSTGRES_PASSWORD` | Local developer password only | `Password=` |

   State explicitly: **the connection string itself is not built in CRM-197** — the mapping is documented so CRM-106 stays compatible. State explicitly that **no production credential is required or accepted** in `env/backend.env`.

4. **Common commands table (line 98)** — replace the preview row `| Infrastructure up / down | Start PostgreSQL and friends via Docker Compose | CRM-197 |` with real rows marked `available today`: `docker compose up -d`, `docker compose ps`, `docker compose down`, and `docker compose down -v` (annotated **destructive**).

Per the docs update rule (`README.md:118-120`), these README edits ship in the **same** pull request as `docker-compose.yml`. **No ADR is required** — this story implements `ADR-002`, it does not change an architectural decision.

### 5 — Confirm ignore rules; add nothing to the backend

**File: `.gitignore`** — verify only. `*.env` / `!*.env.example` (~lines 41–43) already keeps `env/backend.env` out of git, and the CRM-197 block (~lines 52–54) already ignores `.docker/` and `docker-compose.override.yml`. **Make no edit unless `git check-ignore -v env/backend.env` fails.**

**No backend code changes required.** Do not add `ConnectionStrings` to `appsettings.json` / `appsettings.Development.json`, do not add an Npgsql or EF Core package reference, and do not modify `src/backend/src/Api/SquadCrm.Api/Program.cs` — its health checks stay liveness-only (`Program.cs:54-55`).

**No frontend changes required.** Do not touch `src/frontend/**` or `env/frontend.env.example`.

**No `scripts/` entry points.** `scripts/` is empty and belongs to CRM-203; the intake requires plain cross-platform `docker compose` commands, not shell wrappers.

---

## Edge Cases & Failure Modes

- **Port 5432 already in use** (a locally installed PostgreSQL, or another project's container). `docker compose up -d` fails with a bind error. **Expected behaviour:** the developer sets `POSTGRES_PORT` in `env/backend.env` to a free port; the container still listens on `5432` internally, so only the published port changes. Enforced by the `"127.0.0.1:${POSTGRES_PORT:-5432}:5432"` mapping in `docker-compose.yml`. Document this in the README section from Task 4.
- **No `env/backend.env` present.** Compose substitution falls back to the `:-` defaults and the stack starts with developer-safe values. **This is intended** — the README still lists `cp env/backend.env.example env/backend.env` as step 1 so the file exists before CRM-106 needs it.
- **Credentials changed after first start.** PostgreSQL's entrypoint only honours `POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_DB` when initialising an **empty** data directory. Changing them later leaves the old credentials in the existing volume and the health check fails with an authentication error. **Expected behaviour:** documented in the README — changing credentials requires `docker compose down -v` (destructive) followed by `up -d`.
- **`down -v` run by accident.** All local data is unrecoverable. Mitigated only by documentation: the destructive command lives in its **own** fenced block with an explicit "There is no undo" warning (Task 4), never inline in the routine stop instructions.
- **Health check reported unhealthy at start.** `pg_isready` fails during initdb on first run. **Expected behaviour:** `start_period: 30s` absorbs this; the service becomes `healthy` afterwards. If it remains unhealthy past `retries: 5`, `docker compose logs postgres` is the documented diagnostic.
- **Unescaped variable in `healthcheck.test`.** A single `$` makes Compose resolve the user/database at parse time, which both bakes the values into the resolved config and breaks when the env file changes. Guarded by the `$${...}` form in `docker-compose.yml` and verified by the `docker compose config` check in Verification Step 2.
- **Windows/macOS line endings and paths.** All commands are plain `docker compose`; the only shell-specific line is the `COMPOSE_ENV_FILES` export, and the PowerShell equivalent is documented alongside it (Task 4).
- **Compose v1 (`docker-compose`, hyphenated).** `COMPOSE_ENV_FILES` and the top-level `name:` key are v2+ features. **Expected behaviour:** the README states Compose v2 or newer is required and shows the `--env-file` alternative.
- **Chosen image tag does not exist / fails to pull.** A tag guessed from `PG_VERSION` or from another project may not be published. **Expected behaviour:** Task 1 Step 3 (`docker pull`) is the gate — a tag that does not pull never reaches `docker-compose.yml`. **Do not copy the `<verified-tag-from-task-1>` placeholder literally.**
- **Container not found by name.** Because no `container_name:` is set, commands that address `squadcrm-postgres` directly (`docker exec squadcrm-postgres …`) will fail. **Expected behaviour:** always use the Compose service name (`docker compose exec postgres …`). Compose-generated names also differ between clones and worktrees — that is intentional and avoids collisions.
- **Container gone after a Docker Engine or machine restart.** With no restart policy, PostgreSQL does **not** come back automatically. **Expected behaviour:** the developer runs `docker compose up -d` again; the named volume means no data is lost. Document this in the README section from Task 4.

---

## Test Plan

There is no test runner for infrastructure in this repository — CRM-202 owns the testing foundation, and this story must not create one. Verification is therefore **repeatable smoke checks plus regression guards**, all runnable from the repository root.

1. **Smoke — config validity.** `docker compose config` exits `0`, prints no `version is obsolete` warning, and shows the pinned image tag with every `${...}` resolved.
2. **Smoke — startup and health.** `docker compose up -d`, then poll `docker compose ps` until `postgres` reports `healthy`.
3. **Smoke — host reachability.** From the host, connect on the documented coordinates and confirm the configured database and user:
   ```bash
   docker compose exec postgres psql -U squadcrm -d squadcrm -c '\conninfo'
   PGPASSWORD=change-me psql -h localhost -p 5432 -U squadcrm -d squadcrm -c 'select 1;'   # host client, if installed
   ```
4. **Smoke — persistence across a normal stop.** Write, stop, restart, read back:
   ```bash
   docker compose exec postgres psql -U squadcrm -d squadcrm -c 'create table crm197_probe(id int); insert into crm197_probe values (1);'
   docker compose down
   docker compose up -d
   docker compose exec postgres psql -U squadcrm -d squadcrm -c 'select count(*) from crm197_probe;'   # expect 1
   ```
5. **Smoke — explicit reset destroys data.** `docker compose down -v && docker compose up -d`, then `select count(*) from crm197_probe;` must fail with "relation does not exist". **Drop `crm197_probe` (or reset) afterwards — the probe table must not be left behind for CRM-106.**
6. **Smoke — port override.** Set `POSTGRES_PORT=55432` in `env/backend.env`, `docker compose up -d`, confirm `docker compose ps` publishes `127.0.0.1:55432->5432/tcp`. Revert afterwards.
7. **Regression — backend unchanged.** `cd src/backend && dotnet build && dotnet test` still passes, matching the existing suites in `src/backend/tests/SquadCrm.Api.Tests/` and `src/backend/tests/SquadCrm.ArchitectureTests/`. **Do not add tests to either project.**
8. **Regression — frontend unchanged.** `cd src/frontend && npm run build` still succeeds.

---

## Migration / Rollback

- **Migration:** none. One new root file (`docker-compose.yml`), one comment edit in `env/backend.env.example`, README edits. No schema, no runtime code, no public contract change.
- **Rollback:** `docker compose down -v`, delete `docker-compose.yml`, revert the `env/backend.env.example` comment and the README edits. Nothing in the application depends on the database yet, so rollback is complete and side-effect free.
- **Half-applied risk:** a committed `docker-compose.yml` whose image tag was never pulled, or README commands that reference a service name that does not match the file. Guard by running `docker compose config` and Verification Steps 2–4 before pushing.

---

## Verification Steps

1. **Environment prepared:** `cp env/backend.env.example env/backend.env` and `git check-ignore -v env/backend.env` confirms it is ignored.
2. **Compose validates:** from the repository root, `docker compose config` exits `0` with no warnings and every variable resolved.
3. **Infrastructure starts healthy:** `docker compose up -d` then `docker compose ps` shows the **`postgres` service** as `running (healthy)` (the container name is Compose-generated; do not assert on it).
4. **Reachable from the host:** Test Plan step 3 succeeds on the documented coordinates; `docker compose port postgres 5432` reports `127.0.0.1:<POSTGRES_PORT>`.
5. **Data survives a normal cycle:** Test Plan step 4 returns `1` after `down` + `up -d`.
6. **Explicit reset clears data:** Test Plan step 5 fails with "relation does not exist" after `down -v`.
7. **Backend builds:** `cd src/backend && dotnet restore && dotnet build && dotnet test` — all pass, unchanged.
8. **Frontend runs:** `cd src/frontend && npm run build` — succeeds, unchanged.
9. **Regression — no scope creep:**
   - `grep -rn "ConnectionStrings\|Npgsql\|EntityFrameworkCore\|Hangfire\|Migration" src/backend/src` → no results.
   - `grep -n "initdb\|init.sql\|create schema\|CREATE TABLE" docker-compose.yml` → no results.
   - `grep -nc "services:" docker-compose.yml` and inspect — exactly one service, `postgres`; no application container, no pgAdmin/Redis/MinIO/Mailhog/Ollama.
   - `grep -n "latest" docker-compose.yml` → no results (image tag is explicitly pinned).
   - `grep -n "container_name\|restart:" docker-compose.yml` → no results.
   - The tag in `docker-compose.yml` matches a tag that `docker pull` succeeded on; re-running `docker pull <that tag>` succeeds on a clean machine.
10. **Regression — no secrets committed:** `git diff` contains no production hostname, password or token; every credential in the diff is a developer-safe local default.
11. **Regression — preserved artefacts:** `git status` shows changes confined to `docker-compose.yml`, `env/backend.env.example` and `README.md`. `docs/`, `CLAUDE.md`, `.claude/`, `.squad/`, `src/backend/**`, `src/frontend/**`, `env/frontend.env.example` unchanged.

---

## Done Criteria

- [ ] `docker-compose.yml` exists at the repository root, declares exactly one service (`postgres`), and `docker compose config` succeeds with no obsolete-`version` warning.
- [ ] The PostgreSQL image is pinned to a **concrete, non-floating tag from a currently supported major**, selected from actually published tags and **verified by a successful `docker pull`**; the tag was not derived from `PG_VERSION` or any other image metadata. `latest` appears nowhere, and no fabricated tag (e.g. `postgres:17.x-alpine`) was used. Digest pinning is not required.
- [ ] No `container_name:` is set — all documented commands address the Compose **service** name `postgres`.
- [ ] No `restart:` policy is set — the local lifecycle is explicitly `docker compose up -d` / `docker compose down`.
- [ ] `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` and the published host port are **environment-configurable** through the existing `env/backend.env` keys; **no competing configuration contract** was created and no key was renamed.
- [ ] No secret is baked into the image or the Compose file; committed templates stay secret-free and `env/backend.env` remains git-ignored.
- [ ] Data persists in the named volume **`squadcrm-pgdata`**; `docker compose down` preserves it and only `docker compose down -v` removes it.
- [ ] A **meaningful health check** based on the configured user and database (`pg_isready -U … -d …`) is configured, and the service reaches `healthy`.
- [ ] The port is published on **`127.0.0.1` only**; no machine-specific IP is hard-coded anywhere.
- [ ] Root `README.md` documents copy-pasteable commands for prepare / start / status-and-health / stop-preserving-data / **explicitly-destructive reset**, with the destructive command clearly marked, plus the Compose-variable → ASP.NET Core configuration mapping table.
- [ ] Local development requires **no production credential** and no locally installed PostgreSQL server.
- [ ] **No** EF Core, migrations, business schemas/tables, init SQL, `ConnectionStrings` configuration or application persistence code was introduced.
- [ ] **No** application containers (Angular or ASP.NET Core) and no unrelated future infrastructure were added to Compose.
- [ ] Existing backend (`dotnet build` / `dotnet test`) and frontend (`npm run build`) behaviour is intact; `docs/adr/**`, `docs/architecture/**`, `.squad/**`, `.claude/**`, `CLAUDE.md`, `src/**` untouched.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 05.**
