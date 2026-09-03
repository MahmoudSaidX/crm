# Story 21 — Dockerize Full Local Application Stack (Story: CRM-205)

## Prerequisites

- **Story 04 completed (CRM-197 — Docker Compose & Local Infrastructure):** root `docker-compose.yml` with the `postgres` service, named volume `squadcrm-pgdata`, and the `env/backend.env` contract already exist. See [`../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md`](../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md). This story **extends** that file; it does not replace it.
- **Story 03 completed (CRM-105 — ASP.NET Core Modular Monolith Foundation):** backend solution baseline, `net10.0`, `global.json` pinned to SDK `10.0.111` (`rollForward: latestFeature`). See [`../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md`](../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md).
- **Story 05 completed (CRM-106 — PostgreSQL + EF Core, schema-per-module):** the backend already reads `POSTGRES_*` via `PostgresConfiguration.ReadPostgresOptions()` and builds `ConnectionStrings:SquadCrmPostgres` at startup; Hangfire's Postgres storage is also wired at startup in `Program.cs`. This story does not change that code.
- **CRM-122 (Create Customer Profile) merged to `main`:** confirms the current module set (`CustomerManagement`, `BranchManagement`, `DepartmentManagement`, `RoleManagement`, `BrandingManagement`, `SystemConfiguration`, `StaffIdentity`, `Audit`, `ArchitectureFixture`) and the current Angular app shape used below.
- Docker Engine + Compose v2 available. Observed at planning time: `Docker version 29.7.2`. Image tags below were verified with `docker manifest inspect` during planning:
  - `mcr.microsoft.com/dotnet/sdk:10.0` — resolves (manifest list).
  - `mcr.microsoft.com/dotnet/aspnet:10.0` — resolves (manifest list).
  - `node:22-alpine` — resolves (manifest index).
  - `nginx:1.27-alpine` — resolves (manifest index).
  - No Docker Hub/MCR tag matches the exact SDK patch `10.0.111` (Microsoft does not publish per-SDK-patch image tags); `global.json`'s own `rollForward: latestFeature` pin inside the `10.0` SDK image is what guarantees the exact toolchain, matching the precedent set in [`../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md`](../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md) Task 1 ("inspect, do not assume", never a fabricated tag). `latest` is never used for any image in this story.

---

## Story Goal

A developer who has cloned the repository and prepared local env files can run **one command from the repository root**, `docker compose up --build`, and get the complete demoable Squad CRM stack:

1. `postgres` (unchanged from CRM-197) reaches `healthy`.
2. A new `backend` service builds the ASP.NET Core API from source and starts once Postgres is healthy, connecting to the Compose Postgres over the Docker network (`POSTGRES_HOST=postgres`), and reaches `healthy` on `/health/ready`.
3. A new `agent-crm` service builds and serves the Agent CRM Angular app on `http://localhost:4200`, calling the backend at `http://localhost:5080` (the **host-published** backend port — never an internal Docker hostname) exactly as it already does outside Docker.
4. A new `customer-portal` service builds and serves the Customer Portal Angular app on `http://localhost:4300`, calling the backend the same way.
5. `docker compose down` stops everything and preserves the `squadcrm-pgdata` volume; `docker compose down -v` remains the documented destructive reset.
6. The existing non-Docker workflow (`dotnet run`, `npm run start:agent-crm`, `npm run start:customer-portal` against a Compose-started Postgres on `localhost:5432`) is untouched and keeps working.

**Explicitly not in scope:** Kubernetes, any cloud/production deployment pipeline, a reverse-proxy/gateway service, Redis/Kafka/RabbitMQ/service discovery, EF Core migration-behaviour changes (the existing explicit `dotnet ef database update` workflow is preserved as-is — see Edge Cases), any application feature change, and exposing PostgreSQL beyond its existing `127.0.0.1` binding.

---

## Context — Read These Files First

1. `docker-compose.yml` (repository root, 39 lines) — the existing `postgres` service, its header comment ("PostgreSQL only... owned by CRM-106"), the `${...:-default}` substitution pattern, the `$${...}` escaping in `healthcheck.test`, and the `squadcrm-pgdata` named volume. This story adds services **into this same file**; it does not create a second Compose file.
2. `env/backend.env.example` — **lines 1–14** (`ASPNETCORE_URLS`, the five `POSTGRES_*` keys, `POSTGRES_VOLUME_NAME`) and **line 24** (`CORS__AllowedOrigins__0=http://localhost:4200`). The container needs `POSTGRES_HOST=postgres`/`POSTGRES_PORT=5432` (Docker-network values) while this file must keep `POSTGRES_HOST=localhost` for host-run apps — override in `docker-compose.yml`, do not edit these two lines here. Add a **second** CORS line (`CORS__AllowedOrigins__1=http://localhost:4300`) so both apps are allowed in both docker and non-docker runs.
3. `src/backend/src/Api/SquadCrm.Api/Program.cs` — **lines 92–115** (CORS built from `Cors:AllowedOrigins`, throws on `*`), **lines 128–150** (`builder.AddSquadCrmPostgres()`, then Hangfire's `UsePostgreSqlStorage` at startup — the backend **cannot start** without reaching Postgres), **lines 205–212** (`/health`, `/health/live`, `/health/ready` mapped; `/health/ready` is tagged `"ready"` and is what `PostgresReadinessHealthCheck` reports on).
4. `src/backend/src/Infrastructure/SquadCrm.Infrastructure.Postgres/PostgresOptions.cs` and `PostgresConfiguration.cs` — the five flat `POSTGRES_*` configuration keys (`HostKey`, `PortKey`, `DatabaseKey`, `UsernameKey`, `PasswordKey`) are read directly from `IConfiguration` (environment variables included) with no `Postgres__` nesting. Confirms the container only needs `POSTGRES_HOST`/`POSTGRES_PORT` overridden — nothing else about this contract changes.
5. `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj` — `Sdk="Microsoft.NET.Sdk.Web"`, the full `ProjectReference` list (`BuildingBlocks`, `Infrastructure.Postgres`, `Infrastructure.FileStorage`, and every `Modules/*` project). A Docker build that restores/publishes **only this project** (not the whole `.sln`) pulls in exactly this reference graph and none of the four test projects.
6. `src/backend/global.json` — SDK `10.0.111`, `rollForward: latestFeature`. The build-stage base image must be an SDK image that satisfies this roll-forward policy (`mcr.microsoft.com/dotnet/sdk:10.0`).
7. `src/frontend/package.json` — **scripts** `start:agent-crm` (`ng serve agent-crm`), `start:customer-portal` (`ng serve customer-portal`), `build:agent-crm`, `build:customer-portal`, `build` (both). **engines**: `node >= 22.12.0`.
8. `src/frontend/angular.json` — **`serve.options.port`**: `agent-crm` → `4200` (~line 73), `customer-portal` → `4300` (~line 173). No `outputPath` override, so `ng build` writes to the Angular CLI application-builder default `dist/<project>/browser/` (verified in this session: `dist/agent-crm/browser/index.html`, `dist/agent-crm/browser/config.json`).
9. `src/frontend/projects/agent-crm/public/config.json` and `src/frontend/projects/customer-portal/public/config.json` — both currently `"apiBaseUrl": "http://localhost:5080"`. This file is copied into `dist/<project>/browser/config.json` by the Angular build (via the `assets` glob in `angular.json`) and fetched **by the browser at runtime** (`src/frontend/projects/platform/src/lib/config/runtime-config-loader.ts`, `RUNTIME_CONFIG_URL` defaults to `config.json`). **Do not change this value and do not template it per-environment** — `localhost:5080` is correct whether the backend runs via `dotnet run` or via the new `backend` Compose service, because both publish the API on host port `5080`.
10. `README.md` — **lines 90–130** (`## Local infrastructure (Docker Compose)` section, Postgres-only today) and the **First-time setup** list (~lines 60–70, step 6 currently only starts Postgres) and the **Common commands** table (~line 197, the `dotnet ef database update` row). Extend, do not duplicate.
11. `src/backend/README.md` — **lines 25–47** (commands table style) and **line 269** (`Program.cs` "contains no `Database.Migrate()`: the application must not silently mutate a database it does not own the schema for") and the `dotnet ef database update --project … --context …` per-module migration workflow (~lines 42, 281–292). This workflow is **unchanged** by this story — see Edge Cases.
12. `.gitignore` — **lines 41–43** (`*.env` / `!*.env.example`) and **lines 52–54** (`# Docker / local infra (CRM-197)`, `.docker/`, `docker-compose.override.yml`). No new ignore rule is needed for `Dockerfile`/`.dockerignore`/`nginx.conf` — they are committed source, not local state.

Grep hints while implementing:

- `grep -n "POSTGRES\|CORS" env/backend.env.example` — the exact keys to reuse/extend.
- `grep -rn "AddSquadCrmPostgres\|UsePostgreSqlStorage" src/backend/src/Api/SquadCrm.Api/Program.cs` — confirms the backend needs a **reachable** Postgres at startup, not just at first request (drives the `depends_on: condition: service_healthy` requirement below).
- `grep -n "outputPath" src/frontend/angular.json` — confirms there is none; the default `dist/<project>/browser` path is what the frontend Dockerfile must reference.
- `find src/backend -iname Dockerfile* ; find src/frontend -iname Dockerfile*` — both empty at planning time; this story creates both.

---

## Product rules (from story)

**Current behaviour:** `docker-compose.yml` starts only `postgres`. The backend is run with `dotnet run --project src/Api/SquadCrm.Api` directly on the host, reading `POSTGRES_HOST=localhost` from `env/backend.env`. Both Angular apps are run with `ng serve` directly on the host. No Dockerfile exists anywhere in the repository.

**New behaviour:**

- `docker-compose.yml` gains three services — `backend`, `agent-crm`, `customer-portal` — alongside the unchanged `postgres` service.
- `backend` builds from a new `src/backend/Dockerfile`, waits for `postgres` to be `healthy` (`depends_on.postgres.condition: service_healthy`), receives `POSTGRES_HOST=postgres` / `POSTGRES_PORT=5432` as **Docker-network** overrides while every other backend setting (Cors, Authentication, FileStorage, BackgroundProcessing, …) loads from the existing `env/backend.env` via `env_file:`, and publishes on `127.0.0.1:${BACKEND_PORT:-5080}` mapped to its internal port `8080`.
- `agent-crm` and `customer-portal` each build from a new shared `src/frontend/Dockerfile` (parameterised by a build arg naming the Angular project), serve the **already-built static files** through `nginx:1.27-alpine` with SPA fallback routing, and publish on `127.0.0.1:4200` and `127.0.0.1:4300` respectively — the same ports developers already use with `ng serve`.
- Both Angular images embed `config.json` with `apiBaseUrl: http://localhost:5080` **unchanged** — the browser (running on the host) reaches the backend through the host-published port, never through the internal Compose service name `backend`.
- `dotnet run` / `ng serve` against a Compose-started Postgres remains fully supported; nothing in this story requires using the new `backend`/`agent-crm`/`customer-portal` services.

---

## Implementation tasks

### 1 — Backend Dockerfile

**Create file: `src/backend/Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1
# Build stage: SDK image satisfies global.json's rollForward: latestFeature
# pin to SDK 10.0.111. No digest/patch tag exists upstream for this SDK
# patch (verified with `docker manifest inspect` — CRM-205 planning); the
# combination of this floating-minor SDK tag and the repo's own global.json
# pin is the same pattern CRM-197 Task 1 used for the postgres image.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore only the API project's own reference graph (BuildingBlocks,
# Infrastructure.*, every Modules/* project) — never the whole .sln, so the
# four test projects never enter the image or the build context.
COPY . .
RUN dotnet restore src/Api/SquadCrm.Api/SquadCrm.Api.csproj
RUN dotnet publish src/Api/SquadCrm.Api/SquadCrm.Api.csproj \
    --no-restore \
    -c Release \
    -o /app/publish

# Runtime stage: aspnet (not sdk) — smaller image, no build tooling.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# curl is required by the Compose healthcheck (Task 3) against /health/ready;
# the aspnet base image does not include it.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Container-internal port. The Compose file (Task 3) publishes this on the
# same host port (5080) that `dotnet run` already uses, so config.json's
# apiBaseUrl (Context item 9) needs no change.
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SquadCrm.Api.dll"]
```

**Create file: `src/backend/.dockerignore`**

```
**/bin/
**/obj/
**/TestResults/
tests/
.vs/
*.user
```

Excluding `tests/` is safe: Task 1's `dotnet restore`/`publish` target only `src/Api/SquadCrm.Api/SquadCrm.Api.csproj`, which does not reference any project under `tests/`.

### 2 — Frontend Dockerfile (shared by both Angular apps)

**Create file: `src/frontend/Dockerfile`**

```dockerfile
# syntax=docker/dockerfile:1
FROM node:22-alpine AS build
WORKDIR /app

# ARG must be redeclared in every stage that uses it (Docker build-arg scoping).
ARG APP_PROJECT

COPY package*.json ./
RUN npm ci

COPY . .
RUN npm run build:${APP_PROJECT}

# nginx serves the already-built static bundle — no Node runtime, no
# production reverse-proxy/gateway behaviour beyond a single SPA vhost.
FROM nginx:1.27-alpine AS final
ARG APP_PROJECT

COPY --from=build /app/dist/${APP_PROJECT}/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
```

**Create file: `src/frontend/nginx.conf`**

```nginx
server {
    listen 80;
    server_name _;

    root /usr/share/nginx/html;
    index index.html;

    # Angular client-side routing: unknown paths fall back to index.html.
    location / {
        try_files $uri $uri/ /index.html;
    }
}
```

**Create file: `src/frontend/.dockerignore`**

```
node_modules/
dist/
.angular/
```

Both `npm run build:agent-crm` and `npm run build:customer-portal` (Context item 7) already run `ng build --configuration=production`; the Dockerfile does not duplicate that flag.

### 3 — Extend the root Compose file

**File: `docker-compose.yml`**

1. Update the header comment (currently "PostgreSQL only. EF Core, schemas, migrations and application persistence are owned by CRM-106; Hangfire by CRM-199; observability by CRM-201.") to state that the file now also runs the backend API and both Angular apps for local development/demo (CRM-205), while EF Core/migrations/business schemas remain owned by the modules as before — do not remove the existing CRM-197 attribution for the `postgres` service.
2. Leave the existing `postgres` service and `volumes:` block byte-for-byte unchanged.
3. Add three services:

```yaml
  backend:
    build:
      context: ./src/backend
      dockerfile: Dockerfile
    env_file:
      - env/backend.env
    environment:
      # Docker-network overrides: env/backend.env keeps POSTGRES_HOST=localhost
      # for host-run `dotnet run`; these two values win here via `environment:`
      # taking precedence over `env_file:`.
      POSTGRES_HOST: postgres
      POSTGRES_PORT: "5432"
      ASPNETCORE_URLS: http://+:8080
    ports:
      - "127.0.0.1:${BACKEND_PORT:-5080}:8080"
    depends_on:
      postgres:
        condition: service_healthy
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health/ready"]
      interval: 10s
      timeout: 5s
      retries: 5
      start_period: 20s

  agent-crm:
    build:
      context: ./src/frontend
      dockerfile: Dockerfile
      args:
        APP_PROJECT: agent-crm
    ports:
      - "127.0.0.1:4200:80"
    depends_on:
      backend:
        condition: service_started

  customer-portal:
    build:
      context: ./src/frontend
      dockerfile: Dockerfile
      args:
        APP_PROJECT: customer-portal
    ports:
      - "127.0.0.1:4300:80"
    depends_on:
      backend:
        condition: service_started
```

Notes the executor must honour:

- **`env_file: - env/backend.env`** loads every existing backend key (Authentication, FileStorage, BackgroundProcessing, CORS, OTLP, …) without re-declaring them one by one in `docker-compose.yml`. This makes `env/backend.env` existing (Task 4 below documents `cp env/backend.env.example env/backend.env` as a hard prerequisite for `docker compose up --build`, not just a fallback) — see Edge Cases.
- `depends_on.postgres.condition: service_healthy` is required, not cosmetic: `Program.cs` calls `UsePostgreSqlStorage` for Hangfire at startup (Context item 3), so the backend **fails to start** if Postgres is not yet accepting connections.
- No `condition: service_healthy` is used for `agent-crm`/`customer-portal` → `backend`: they are static file servers; nothing they serve requires the backend to already be reachable at container-start time, only at first browser call.
- No `container_name:`, no `restart:` on any of the three new services — same rationale CRM-197 already established for `postgres` (Compose-generated names avoid clone/worktree collisions; the local lifecycle stays explicit).
- Ports stay on `127.0.0.1` only, matching the existing `postgres` binding and the "do not expose beyond existing local binding" business rule.

### 4 — Extend the environment contract and README

**File: `env/backend.env.example`** — add one line after the existing `CORS__AllowedOrigins__0=http://localhost:4200` (line 24):

```
CORS__AllowedOrigins__1=http://localhost:4300
```

This is not Docker-specific: it also fixes Customer Portal → backend CORS for the existing non-Docker `ng serve` workflow, satisfying AC 7 in both modes.

**File: `README.md`** — extend the existing `## Local infrastructure (Docker Compose)` section (Context item 10); do not create a second Docker section.

1. Update the section's opening paragraph: it no longer provides "the single local dependency Squad CRM needs today: PostgreSQL" — state that it now also builds and runs the backend API, Agent CRM and Customer Portal.
2. Add a subsection (e.g. `### Full stack`) documenting:
   ```bash
   # 1. Prepare local environment values (once per clone) — now required, not optional,
   #    because the backend container loads every value from this file.
   cp env/backend.env.example env/backend.env

   # 2. Build and start the complete stack
   docker compose up --build

   # 3. Open:
   #    - Agent CRM:        http://localhost:4200
   #    - Customer Portal:  http://localhost:4300
   #    - Backend API health: http://localhost:5080/health/ready

   # 4. Stop, preserving all data
   docker compose down
   ```
   Repeat the existing destructive-reset block (`docker compose down -v`) by reference rather than duplicating the warning text — one sentence: "Stack-wide reset uses the same destructive `docker compose down -v` documented above; it also removes the built application images' containers, not just Postgres."
3. Update **First-time setup** step 6 (Context item 10) to mention that `docker compose up --build` now starts the full stack, keeping the existing `export COMPOSE_ENV_FILES=…` Postgres-only alternative documented for developers who only want the database.
4. Add a short note directly under the new subsection: **"After the first `docker compose up --build` against a fresh `squadcrm-pgdata` volume, apply module migrations from the host once — `dotnet ef database update --project … --context …` per `src/backend/README.md` — before exercising data-backed features. `backend` does not run migrations automatically; see Edge Cases."**
5. Extend the **Common commands** table (Context item 10) with one new row: `docker compose up --build` → "Build and start the full local stack (Postgres + backend + both Angular apps)" → available today.

---

## Edge Cases & Failure Modes

- **`env/backend.env` missing.** Because `backend` now uses `env_file: - env/backend.env` (Task 3), `docker compose up --build` **fails immediately** with a clear "env file … not found" error if the developer skips `cp env/backend.env.example env/backend.env`. This is a deliberate tightening from CRM-197 (where every Postgres value had an in-file default): it is documented as the **first, now-mandatory** step in the README's full-stack subsection (Task 4).
- **Fresh Postgres volume, no migrations applied.** `backend` starts and reports `healthy` on `/health/ready` (that check only opens a connection and runs `SELECT 1` — see `PostgresReadinessHealthCheck.cs`), but any endpoint touching a module's tables (e.g. Customer Management) fails until the developer runs the existing `dotnet ef database update` workflow from the host. **This story does not add automatic migration** — `src/backend/README.md:269` ("the application must not silently mutate a database it does not own the schema for") is preserved unchanged; the README note in Task 4 point 4 makes this explicit so it is not mistaken for a bug.
- **Backend cannot reach Postgres at startup.** `depends_on.postgres.condition: service_healthy` prevents `backend` from starting before Postgres is ready; if Postgres later becomes unreachable (e.g. `docker compose restart postgres`), the backend process — which resolved its connection string once at startup — keeps its existing Npgsql pool and reconnects per Npgsql's own retry behavior; no change from today's non-Docker behavior.
- **Port already bound** (4200, 4300, or 5080 in use by a host-run `ng serve`/`dotnet run` instance from the same clone). `docker compose up --build` fails with a bind error on the conflicting port. Expected: stop the host-run process, or override `BACKEND_PORT` for the backend (`agent-crm`/`customer-portal` ports are fixed at `4200`/`4300` to match the ports already hard-coded in `angular.json`'s `serve` config and in `config.json`'s `apiBaseUrl`; document that overriding them also requires editing both `config.json` files and is out of scope for a quick local override).
- **CORS rejects Customer Portal in an existing local checkout.** A developer who copied `env/backend.env` **before** this story's `CORS__AllowedOrigins__1` addition keeps the old file (env files are git-ignored, never auto-updated). Expected: the browser console shows a CORS error for `http://localhost:4300`; the fix is re-copying or manually adding the new line — call this out in the README note (Task 4).
- **`npm ci` cache-busting.** The frontend Dockerfile's `COPY package*.json ./` before `COPY . .` means `npm ci` only reruns on a rebuild when `package.json`/`package-lock.json` change; a source-only change still triggers a full `npm run build:<project>` layer (no incremental Angular build cache is preserved across `--build` runs) — acceptable for a local/demo story; do not add BuildKit cache-mount tuning.
- **`nginx.conf` and Angular routing.** Without `try_files … /index.html`, a hard browser refresh on a non-root Angular route (e.g. `/customers/new`) returns nginx's default 404. Enforced by the `location /` block in `src/frontend/nginx.conf` (Task 2).
- **Backend healthcheck tool availability.** `curl` is not present in the stock `mcr.microsoft.com/dotnet/aspnet:10.0` image; the `apt-get install curl` step in `src/backend/Dockerfile` (Task 1) is required for the Compose `healthcheck.test` (Task 3) to run at all — omitting it makes the healthcheck itself fail with "executable file not found", masking the real backend status.

---

## Test Plan

There is no application test suite for Docker orchestration itself (CRM-202 owns the backend/frontend testing foundation and is not extended here). Verification is smoke-based, run from the repository root, extending the pattern already established in CRM-197's Test Plan.

1. **Smoke — config validity.** `docker compose config` exits `0` and resolves every variable across all four services (extends CRM-197 Test Plan step 1).
2. **Smoke — full build.** `docker compose build` succeeds for `backend`, `agent-crm`, `customer-portal` (no cached `postgres` build needed).
3. **Smoke — full startup.** `docker compose up --build -d`, then poll `docker compose ps` until `postgres` and `backend` report `healthy`, and `agent-crm`/`customer-portal` report `running`.
4. **Smoke — backend readiness from host.** `curl -f http://localhost:5080/health/ready` returns success and confirms the Postgres check for the reported readiness payload.
5. **Smoke — backend connects to Compose Postgres.** `docker compose logs backend` shows no `PostgreSQL is unavailable` from `PostgresReadinessHealthCheck`; `curl http://localhost:5080/health/ready` payload shows `"postgres"` entry healthy.
6. **Smoke — Agent CRM loads.** `curl -f http://localhost:4200/` returns the built `index.html`; open in a browser and confirm the shell renders (no config-load error banner from `runtime-config-loader.ts`).
7. **Smoke — Customer Portal loads.** `curl -f http://localhost:4300/` returns the built `index.html`.
8. **Smoke — Agent CRM → backend call.** After applying migrations once (`dotnet ef database update` per module, from the host, against the Compose Postgres — see Edge Cases), log in through Agent CRM at `http://localhost:4200` and load a list backed by the API (e.g. the customer list added in CRM-122); confirm a network call to `http://localhost:5080` succeeds in the browser devtools, not a CORS or connection error.
9. **Smoke — normal shutdown and restart preserve data.** `docker compose down`, then `docker compose up -d` (no rebuild); confirm `postgres` volume data and any rows written in step 8 are still present via `docker compose exec postgres psql -U squadcrm -d squadcrm -c 'select 1;'` plus a re-check of the same Agent CRM list.
10. **Smoke — destructive reset.** `docker compose down -v`; confirm the named volume `squadcrm-pgdata` no longer exists (`docker volume ls | grep squadcrm-pgdata` returns nothing).
11. **Regression — non-Docker workflow intact.** With the Compose stack stopped, run `cd src/backend && dotnet build && dotnet test tests/SquadCrm.Api.Tests tests/SquadCrm.ArchitectureTests` and, separately, `docker compose up -d postgres && cd src/backend && dotnet run --project src/Api/SquadCrm.Api` plus `cd src/frontend && npm run start:agent-crm` — all succeed exactly as before this story.
12. **Regression — formatting gates.** `npm run format:check --prefix src/frontend` and `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` both pass.

---

## Migration / Rollback

- **Migration:** none to application data. New files only: `docker-compose.yml` gains three services (existing `postgres` service and volume untouched), `src/backend/Dockerfile`, `src/backend/.dockerignore`, `src/frontend/Dockerfile`, `src/frontend/nginx.conf`, `src/frontend/.dockerignore`; one line added to `env/backend.env.example`; README additions.
- **Rollback:** `docker compose down` (or `down -v` if the new services were ever started against a volume the developer wants clean), remove the three new services from `docker-compose.yml`, delete the four new Dockerfile/related files, revert the one `env/backend.env.example` line and the README additions. `postgres` and the existing non-Docker workflow are unaffected throughout.
- **Half-applied risk:** a `backend` service defined without the `depends_on.postgres.condition: service_healthy` gate would start Hangfire's `UsePostgreSqlStorage` against an unready Postgres and crash-loop; a `env/backend.env` missing the new `CORS__AllowedOrigins__1` line after a partial rollout would silently block Customer Portal API calls with a CORS error rather than a startup failure — both are covered by Verification Steps 3 and 8 below before this is considered done.

---

## Verification Steps

1. **Compose validates:** `docker compose config` — exits `0`, four services resolved (`postgres`, `backend`, `agent-crm`, `customer-portal`).
2. **Full stack builds and starts:** `docker compose up --build -d` — `docker compose ps` shows `postgres` and `backend` `healthy`, `agent-crm`/`customer-portal` `running`.
3. **Backend reachable and connected to Postgres:** `curl -f http://localhost:5080/health/ready` succeeds (Test Plan steps 4–5).
4. **Agent CRM reachable and calls the backend:** Test Plan steps 6 and 8 succeed through an actual browser session.
5. **Customer Portal reachable:** Test Plan step 7 succeeds.
6. **Shutdown and data persistence:** Test Plan step 9 succeeds.
7. **Destructive reset documented and working:** Test Plan step 10 succeeds.
8. **Non-Docker workflow regression:** Test Plan step 11 succeeds.
9. **Quality gates:** Test Plan step 12 succeeds.
10. **Regression — no scope creep:**
    - `grep -n "container_name\|restart:" docker-compose.yml` → no results for the three new services (matches the existing `postgres` convention).
    - `grep -rn "Kubernetes\|nginx.*proxy_pass\|Redis\|Kafka\|RabbitMQ" docker-compose.yml src/frontend/nginx.conf` → no results.
    - `git status` shows changes confined to `docker-compose.yml`, `env/backend.env.example`, `README.md`, `src/backend/Dockerfile`, `src/backend/.dockerignore`, `src/frontend/Dockerfile`, `src/frontend/nginx.conf`, `src/frontend/.dockerignore`. No file under `src/backend/src/**` (application code), `docs/adr/**`, `.squad/**`, `.claude/**`, or `CLAUDE.md` is touched.
11. **No secrets committed:** `git diff` contains no production hostname, password or signing key — only developer-safe defaults and structural Compose/Dockerfile content.

---

## Done Criteria

- [ ] `docker compose up --build` from the repository root starts PostgreSQL, the backend API, Agent CRM and Customer Portal with no other manual step besides `cp env/backend.env.example env/backend.env`.
- [ ] `backend`, `agent-crm`, `customer-portal` are new services in the **existing** root `docker-compose.yml`; the `postgres` service and `squadcrm-pgdata` volume are unchanged.
- [ ] The backend container connects to Postgres using the Docker-network hostname `postgres` (`POSTGRES_HOST=postgres`, `POSTGRES_PORT=5432`), overridden in `docker-compose.yml`, while `env/backend.env`/`env/backend.env.example` keep `POSTGRES_HOST=localhost` for host-run apps.
- [ ] Both Angular apps' `config.json` (`apiBaseUrl`) keep pointing at the **host-published** `http://localhost:5080` — never at the internal service name `backend` — so the browser can resolve it.
- [ ] `backend` only starts after `postgres` is `healthy` (`depends_on.postgres.condition: service_healthy`), and itself exposes a Compose healthcheck against `/health/ready`.
- [ ] Agent CRM (`http://localhost:4200`) can make a real, successful call to the backend API through the browser (Test Plan step 8).
- [ ] Customer Portal (`http://localhost:4300`) is CORS-allowed and reachable; a representative call is verified if one exists at implementation time.
- [ ] `docker compose down` preserves `squadcrm-pgdata`; `docker compose down -v` remains the documented destructive reset, unchanged in behavior from CRM-197.
- [ ] The existing non-Docker workflow (`dotnet run`, `npm run start:agent-crm`, `npm run start:customer-portal`) still works unmodified.
- [ ] No secret or production credential is committed; every new value in `docker-compose.yml`/`Dockerfile`s is a developer-safe local default or structural configuration.
- [ ] README documents prerequisites, `docker compose up --build`, the four localhost URLs, `docker compose down`, and `docker compose down -v` (marked destructive), extending the existing Docker section rather than duplicating it.
- [ ] `npm run format:check --prefix src/frontend` and `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` both pass.
- [ ] No Kubernetes, reverse-proxy/gateway, Redis/Kafka/RabbitMQ, or other unrelated infrastructure was introduced.
- [ ] Overview `.squad/plans/dockerize-full-local-application-stack/00-overview.md` updated with this story.
