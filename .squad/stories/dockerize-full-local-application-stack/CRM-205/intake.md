# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/dockerize-full-local-application-stack/CRM-205/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Dockerize Full Local Application Stack
- **Feature slug (folder under `plans/`):** `dockerize-full-local-application-stack`

## Tracker (metadata only)

- **Tracker type:** `Linear`
- **Work item id:** `CRM-205`
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** `foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
Dockerize Full Local Application Stack
```

---

## Description

```
## User Story
As a developer, I want to run the complete Squad CRM stack (PostgreSQL, backend API, Agent CRM, Customer Portal) with a single `docker compose up --build` from the repository root, so I can demo and manually inspect implemented work without juggling separate terminal processes.

## Business Rules
- Reuse and extend the existing root `docker-compose.yml` (from CRM-197) rather than introducing a competing orchestration system unless the repository proves that impossible.
- Preserve existing PostgreSQL behavior and persistence.
- Keep the solution local-first and free of mandatory paid/cloud services.
- No Kubernetes, no reverse-proxy platform unless technically necessary.
- No Redis, Kafka, RabbitMQ, service discovery, or unrelated infrastructure.
- No production deployment pipeline.
- No application feature changes.
- No unrelated refactoring.
- Do not expose PostgreSQL beyond the existing intended local binding without a concrete reason.
- Do not bake secrets into Docker images.
- Prefer existing runtime configuration/environment mechanisms.
- Keep the implementation simple enough for local development and demos.

## Fields Dictionary
No user-facing fields.
```

---

## Acceptance criteria

```
1. PostgreSQL starts through Docker Compose.
2. ASP.NET Core API starts through Docker Compose.
3. Agent CRM starts through Docker Compose.
4. Customer Portal starts through Docker Compose.
5. Services use the correct Docker-network addresses internally.
6. Agent CRM can successfully call the backend API.
7. Customer Portal is configured to call the backend API when applicable.
8. Backend connects successfully to the Compose PostgreSQL instance.
9. Startup ordering/health dependencies are handled sufficiently so normal startup does not depend on manual timing.
10. Existing PostgreSQL persistent volume behavior remains intact.
11. Existing local non-Docker development workflow remains usable.
12. No credentials/secrets are committed.
13. Required local configuration is documented using existing env/config conventions.
14. After startup, the applications are accessible from the host on documented localhost ports.
15. The complete stack can be stopped with `docker compose down`.
16. A clean local reset remains possible with `docker compose down -v`.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** `CRM-197` — Docker Compose & Local Infrastructure (Done; established PostgreSQL-only Compose file, root `docker-compose.yml`, `env/backend.env.example`). `CRM-105` — ASP.NET Core Modular Monolith baseline (net10.0). `CRM-122` — Create Customer Profile (latest merged feature, confirms current backend/frontend shape).
- **Depends on code areas or other stories:** root `docker-compose.yml` and `env/backend.env(.example)` from CRM-197; `src/backend/src/Api/SquadCrm.Api` (net10.0, `ASPNETCORE_URLS=http://localhost:5080`, health endpoints `/health`, `/health/live`, `/health/ready` already mapped in `Program.cs`); `src/frontend` Angular workspace (`agent-crm` served on port 4200, `customer-portal` on port 4300, both via `ng serve`/`ng build`); runtime config pattern in `src/frontend/projects/platform/src/lib/config/runtime-config-loader.ts` which fetches `public/config.json` (`apiBaseUrl` currently `http://localhost:5080` in both `agent-crm` and `customer-portal`) at browser runtime — this must keep resolving a **host-accessible** URL, never an internal Docker service name.

## Extra notes (optional)

- No backend or frontend Dockerfiles currently exist anywhere in the repo — both need to be added.
- Backend `CORS__AllowedOrigins__0=http://localhost:4200` is currently the only allowed origin in `env/backend.env.example`; Customer Portal's origin (`http://localhost:4300`) will also need to be allowed if it is to call the API from the browser.
- `env/backend.env.example` already documents POSTGRES_HOST=localhost for host-run apps; the backend container will need `POSTGRES_HOST=postgres` (the Compose service name) instead — this must be layered in without breaking the existing non-Docker workflow that still relies on `localhost`.
- `docker-compose.yml` header comment currently says "PostgreSQL only... EF Core... owned by CRM-106" — this comment will need updating since this story deliberately adds the app containers CRM-197 explicitly deferred.
- README.md already has a "Local infrastructure (Docker Compose)" section (Postgres-only) that documents prerequisites, start/stop/reset commands, and a variable mapping table — extend it rather than duplicating a second Docker section.
- Existing non-Docker developer workflow (`dotnet run`, `npm run start:agent-crm`, `npm run start:customer-portal` against a Compose-started Postgres on localhost:5432) must keep working unmodified.

## Technical hints (optional)

- Repo root: `.`. Primary backend language: C# / .NET 10 (`net10.0`). Primary frontend language: TypeScript / Angular 20.
- Existing root `docker-compose.yml` service: `postgres` (image `postgres:18.6-alpine3.24`, named volume `squadcrm-pgdata`, healthcheck via `pg_isready`, published on `127.0.0.1:${POSTGRES_PORT:-5432}`).
- Backend solution: `src/backend/SquadCrm.sln`; API project: `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj` (SDK `Microsoft.NET.Sdk.Web`). Health endpoints already mapped: `/health`, `/health/live`, `/health/ready`.
- Frontend workspace: `src/frontend` (npm workspace with Angular CLI, projects `agent-crm` port 4200, `customer-portal` port 4300, shared libs `platform` and `shared-ui`). Build scripts: `npm run build:agent-crm`, `npm run build:customer-portal`. Runtime config file per app: `public/config.json` with `apiBaseUrl`.
- `env/` folder convention: `env/backend.env.example` (committed, secret-free) → developer copies to `env/backend.env` (gitignored) with real local values; `env/frontend.env.example` also exists.
- Quality gates referenced by the workflow: `npm run format:check --prefix src/frontend`; `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes`.

## Out of scope

- Kubernetes or any cloud/production deployment pipeline.
- Reverse-proxy platform (nginx-as-gateway, Traefik, etc.) unless proven technically necessary for serving the Angular apps.
- Redis, Kafka, RabbitMQ, service discovery, or any infrastructure unrelated to this stack.
- EF Core migrations behavior changes beyond what is minimally needed for reliable container startup (do not introduce automatic destructive migrations/resets; preserve the existing explicit migration workflow unless a minimal, safe, local-only improvement is clearly justified — escalate if this becomes an architecture/data-integrity decision).
- Any application feature change or unrelated refactoring.
- Exposing PostgreSQL beyond its existing local binding.
- Baking secrets into Docker images or committing real credentials.
