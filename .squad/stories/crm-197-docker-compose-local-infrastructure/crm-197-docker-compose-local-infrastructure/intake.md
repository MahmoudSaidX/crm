# Story intake

-   Folder:
    `.squad/stories/docker-compose-local-infrastructure/docker-compose-local-infrastructure/intake.md`
-   Do **not** rely on external links. The planner reads this file and
    `attachments/` only.
-   This is input to squad-kit's plan generator, not an implementation
    prompt.

------------------------------------------------------------------------

## Feature

-   **Feature name (display):** Docker Compose & Local Infrastructure
-   **Feature slug (folder under `plans/`):**
    `docker-compose-local-infrastructure`

## Tracker (metadata only)

-   **Tracker type:** `Linear`
-   **Work item id:** `CRM-197`
-   **Work item type:** `Story`
-   **Status:** `Todo`
-   **Assignee:** `Mahmoud Said`
-   **Labels:** `foundation`

## Title

``` text
[Sprint 0] Docker Compose & Local Infrastructure
```

## Description

``` md
## User Story
As a developer, I want local infrastructure started through Docker Compose so that Squad CRM dependencies are reproducible across machines.

## Business Rules
- Infrastructure configuration must be environment-driven.
- Local providers/emulators may replace external production providers where practical.
- Persistent business data must not depend on application container lifecycle.

## Fields Dictionary
No user-facing fields.
```

## Acceptance criteria

``` md
- [ ] Docker Compose starts PostgreSQL and all agreed local infrastructure dependencies.
- [ ] Applications can connect using environment configuration.
- [ ] Persistent volumes and health checks are configured where appropriate.
- [ ] README contains start/stop/reset commands.
- [ ] Local development does not require production credentials.
```

## Attachments

None.

## Dependencies

-   **Blocked by / related ids:** `CRM-107` --- Repository & Developer
    Workflow --- completed.
-   **Depends on code areas or other stories:** repository/environment
    conventions from CRM-107 and backend configuration contract from
    CRM-105.

### Stories blocked by this story

-   `CRM-106` --- PostgreSQL + EF Core + Schema-per-Module
-   `CRM-199` --- Hangfire Background Processing Foundation
-   `CRM-201` --- OpenTelemetry, Structured Logging & Health Checks
-   `CRM-202` --- Automated Testing & Architecture Tests

## Extra notes (optional)

-   This story establishes local dependency infrastructure, not
    application persistence.
-   PostgreSQL is the required local service.
-   Do not add EF Core, migrations, schemas, business tables,
    repositories or database application code; CRM-106 owns those.
-   Do not containerize Angular or ASP.NET Core applications merely for
    this story. Developers can run applications directly with their
    normal commands.
-   Do not add production deployment infrastructure or production
    credentials.
-   Do not pull future services into Compose unless they are already
    explicitly required at this stage.
-   Preserve existing frontend/backend foundations, `.claude/`,
    `.squad/`, `docs/` and `CLAUDE.md`.

## Technical hints (optional)

-   Repo/root: `.`
-   Local orchestration: Docker Compose.
-   Required service: PostgreSQL.
-   Environment contract: `env/backend.env.example`.
-   Backend baseline from CRM-105: `.NET 10 / net10.0`.
-   EF Core persistence is deferred to CRM-106.

### Minimum topology

``` text
Developer machine
├── Angular apps (normal npm/ng commands)
├── ASP.NET Core API (normal dotnet commands)
└── Docker Compose
    └── PostgreSQL
        ├── persistent named volume
        └── health check
```

### PostgreSQL

-   Use an official supported PostgreSQL image with an explicit pinned
    major/version policy; do not use `latest`.
-   Database name, username, password and published host port must be
    environment-configurable.
-   Local defaults may be developer-safe values, but production
    credentials must never be required or committed.
-   Do not bake secrets into the image or Compose file.
-   Persist data in a named Docker volume.
-   Add a meaningful PostgreSQL health check based on configured
    database/user values.
-   Do not create business schemas/tables/init SQL.
-   Do not run EF migrations from the PostgreSQL container.

### Environment configuration

-   Keep committed environment templates secret-free; actual local
    values remain gitignored.
-   Prefer extending `env/backend.env.example` rather than creating
    competing configuration contracts.
-   The future ASP.NET Core connection-string configuration must be
    compatible with the Compose coordinates, but CRM-197 must not add
    EF/database access code.
-   Document any mapping between Compose variable names and ASP.NET Core
    configuration names explicitly.

### Networking

-   Host-run applications connect through the published localhost port.
-   Do not hard-code machine-specific IPs.
-   Avoid exposing PostgreSQL beyond what local development requires.

### Persistence/reset behavior

-   Normal `docker compose down` preserves the named volume.
-   A separate explicit reset command may remove local data/volume.
-   The destructive reset command must be clearly documented.

### Health

-   PostgreSQL must have a Compose health check.
-   Do not expand this into CRM-201's observability/readiness scope.

### Developer workflow

README must document copy-pasteable commands for: - preparing local
environment values; - starting infrastructure; - checking
status/health; - stopping while preserving data; - intentionally
resetting local persisted data.

### Cross-platform/reproducibility

-   Prefer plain cross-platform `docker compose` commands over
    shell-specific automation.
-   Do not require a locally installed PostgreSQL server when Docker is
    available.
-   Pin important service image versions.

### Verification expected

The generated plan must verify: - `docker compose config` succeeds; -
PostgreSQL starts and reaches healthy state; - PostgreSQL is reachable
from the host using documented coordinates; - written data survives
normal down/start cycles; - explicit reset removes local persisted data
as documented; - no production credentials are required; - no EF Core,
migrations, business schemas/tables or application persistence code are
introduced; - no application containers or unrelated future
infrastructure are introduced; - existing frontend/backend build/test
behavior remains intact.

## Out of scope

-   EF Core and migrations.
-   Module-owned schemas/tables (`CRM-106`).
-   Application database access.
-   Outbox/event persistence (`CRM-198`).
-   Hangfire (`CRM-199`).
-   Full observability (`CRM-201`).
-   Full testing foundation (`CRM-202`).
-   File storage (`CRM-200`) unless separately established as an agreed
    dependency.
-   Email/WhatsApp/SMS emulators.
-   Angular or ASP.NET Core application containers.
-   Kubernetes/cloud/production deployment infrastructure.