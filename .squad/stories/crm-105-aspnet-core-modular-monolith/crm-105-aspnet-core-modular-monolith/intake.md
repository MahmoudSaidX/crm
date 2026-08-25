# Story intake

Fill this template for each story you want planned. Keep it
copy-paste-friendly: the planner reads **this file and the files in
`attachments/`**, nothing else.

-   Folder:
    `.squad/stories/aspnet-core-modular-monolith-foundation/aspnet-core-modular-monolith-foundation/intake.md`
-   Binaries: put them in `attachments/` next to this file and list them
    below.
-   Do **not** rely on external links. Paste the content you want
    considered.

This is **not** an implementation prompt. It is input to squad-kit's
plan-generation prompt.

------------------------------------------------------------------------

## Feature

-   **Feature name (display):** ASP.NET Core Modular Monolith Foundation
-   **Feature slug (folder under `plans/`):**
    `aspnet-core-modular-monolith-foundation`

## Tracker (metadata only)

-   **Tracker type:** `Linear`
-   **Work item id:** `CRM-105`
-   **Work item type:** `Story`
-   **Status:** `Todo`
-   **Assignee:** `Mahmoud Said`
-   **Labels:** `foundation`

------------------------------------------------------------------------

## Title

``` text
[Sprint 0] ASP.NET Core Modular Monolith Foundation
```

## Description

``` md
## User Story
As a developer, I want the ASP.NET Core backend established as a modular monolith so that CRM capabilities have clear boundaries while remaining simple to deploy and operate.

## Business Rules
- Backend architecture is a Modular Monolith, not microservices for the MVP.
- Modules communicate through public contracts/events, not direct access to another module's internals.
- Cross-cutting infrastructure must remain reusable without owning business rules.
- APIs must return consistent errors and correlation information.

## Fields Dictionary
No business data-entry fields in this story.
```

## Acceptance criteria

``` md
- [ ] ASP.NET Core Web API solution builds and runs locally.
- [ ] Business capabilities are separated into explicit modules with controlled dependencies.
- [ ] Dependency injection, environment configuration, OpenAPI, CORS and global exception handling are configured.
- [ ] Standard API error contract and validation pipeline are available.
- [ ] Health endpoint is exposed.
- [ ] Architecture rules can be verified automatically.
```

## Attachments

None.

## Dependencies

-   **Blocked by / related ids:** `CRM-107` --- Repository & Developer
    Workflow --- completed.
-   **Depends on code areas or other stories:** repository baseline from
    CRM-107. CRM-104 frontend is independent.

### Stories blocked by this story

`CRM-106`, `CRM-198`, `CRM-200`, `CRM-201`, `CRM-202`, `CRM-204`,
`CRM-110`, `CRM-192`.

## Extra notes (optional)

-   Establish the backend solution/module architecture and minimum
    API-host foundation only; do not implement CRM business
    capabilities.
-   PostgreSQL + EF Core is selected, but persistence/schema-per-module
    belongs to `CRM-106`.
-   Events/outbox belongs to `CRM-198`; full observability to `CRM-201`;
    full testing strategy to `CRM-202`; security/auth to
    `CRM-204`/`CRM-110`.
-   Existing frontend, `.claude/`, `.squad/`, `docs/`, `CLAUDE.md` and
    CRM-107/104 outputs must be preserved.

## Technical hints (optional)

-   Repo/root: `.`
-   Backend area: expected under `src/backend/`.
-   Platform: `.NET / ASP.NET Core`
-   Architecture: `Modular Monolith`
-   Future persistence: `PostgreSQL + EF Core`
-   References: `docs/adr/ADR-001-modular-monolith.md`,
    `docs/adr/ADR-003-api-errors.md`,
    `docs/architecture/architecture.md`, `docs/architecture/modules.md`,
    `CLAUDE.md`.

### Solution direction

Use current supported .NET conventions and the smallest structure that
genuinely enforces these conceptual boundaries:

``` text
Backend
├── API/Host
├── minimal shared technical building blocks
└── Modules
    └── <BusinessCapability>
        ├── public contract / module entry point
        └── internal implementation
```

Do not pre-create empty Domain/Application/Infrastructure projects for
every future module. A minimal representative/sample module or
architecture fixture may prove boundaries without inventing CRM
behavior.

### Module-boundary rules

-   A module owns its internals and later its private
    persistence/schema.
-   Other modules cannot reference internal implementation types.
-   Cross-module collaboration uses intentional public contracts/events.
-   API host composes modules but owns no business rules.
-   Shared building blocks contain only genuinely cross-cutting
    technical abstractions and never become a business-model dumping
    ground.

### .NET version

Use a currently supported .NET version compatible with the developer
environment/repository. Do not upgrade unrelated machine tooling merely
to chase the newest SDK. Pin the SDK (for example `global.json`) when
useful for reproducibility.

### API host foundation

Establish only: DI/module registration, standard
environment/configuration loading, OpenAPI, configuration-driven CORS,
global exception handling, stable API error contract with safe
correlation information, validation extension point, and a minimal
health endpoint.

Do not add authentication, authorization, sessions, persistence,
Hangfire, Outbox or business APIs.

### Error contract

Prefer current ASP.NET Core standards such as Problem Details / RFC
9457-compatible responses where suitable instead of an unnecessary
custom envelope. Never leak stack traces/internal exception details.

### Validation

Establish the mechanism/pipeline for later endpoints without inventing
business validators. Do not select a third-party validation library
solely by popularity; justify it if used and keep business rules in
their owning capability.

### Health

Expose minimal application/liveness health only. Database/provider
readiness checks belong to stories that introduce those dependencies.

### Architecture verification

Automated verification must prove at least: - module internals cannot be
referenced by another module; - shared technical building blocks do not
depend on business modules; - modules do not depend back on the API
host. Keep it minimal; CRM-202 expands the testing strategy later.

### Configuration/secrets

Use standard ASP.NET Core configuration precedence. Do not commit local
secrets. `env/backend.env.example` remains the operator/developer
contract where applicable. Origins/URLs must be configurable.

### Verification expected

Plan commands/checks proving restore, clean build, local host startup,
OpenAPI, configured CORS, health response, safe exception Problem
Details with correlation/trace info, standard validation error shape,
architecture-boundary verification, and absence of accidental
PostgreSQL/EF Core/auth/Hangfire/Outbox/business/provider
implementation.

## Out of scope

-   CRM business modules/features.
-   PostgreSQL/EF Core/schema-per-module (`CRM-106`).
-   Events/Outbox (`CRM-198`).
-   Hangfire/background infrastructure.
-   File storage (`CRM-200`).
-   Full observability (`CRM-201`).
-   Full testing foundation (`CRM-202`) beyond CRM-105's minimum
    verification.
-   Security/auth/session (`CRM-204`, `CRM-110`).
-   Integration framework (`CRM-192`) or external providers.
-   Production deployment infrastructure.
-   Microservices/service discovery/distributed messaging for the MVP.