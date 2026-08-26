# Story intake

Fill this template for each story you want planned. Keep it
copy-paste-friendly: the planner reads **this file and the files in
`attachments/`**, nothing else.

-   Folder:
    `.squad/stories/crm-106-postgresql-ef-core-schema-per-module/crm-106-postgresql-ef-core-schema-per-module/intake.md`
-   Binaries (screenshots, PDFs, exports): put them in `attachments/`
    next to this file and list them below.
-   Do **not** rely on external links (tracker URLs, wiki, chat) --- the
    planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is input to the
plan-generation meta-prompt bundled with squad-kit.

------------------------------------------------------------------------

## Feature

-   **Feature name (display):** PostgreSQL + EF Core + Schema-per-Module
-   **Feature slug (folder under `plans/`):**
    `crm-106-postgresql-ef-core-schema-per-module`

## Tracker (metadata only)

-   **Tracker type:** `Linear`
-   **Work item id:** `CRM-106`
-   **Work item type:** `Story`
-   **Status:** `Todo`
-   **Assignee:** `Mahmoud Said`
-   **Labels:** `foundation`

------------------------------------------------------------------------

## Title

``` text
[Sprint 0] PostgreSQL + EF Core + Schema-per-Module
```

------------------------------------------------------------------------

## Description

``` md
## User Story

As a developer, I want PostgreSQL and EF Core configured with module-owned schemas so that persistence follows the modular monolith boundaries.

## Business Rules

- PostgreSQL is the system database; SQL Server is not used.
- A module may not directly query or mutate another module's private tables.
- Cross-module workflows use public contracts/events.
- Schema changes must be migration-driven and source controlled.

## Fields Dictionary

No user-facing fields.

| Field | Meaning |
| --- | --- |
| ConnectionString | Secret/environment-derived PostgreSQL connection configuration used by the backend. |
| ModuleSchema | PostgreSQL schema owned exclusively by a module. |
```

------------------------------------------------------------------------

## Acceptance criteria

``` md
- [ ] Backend connects to PostgreSQL using environment-based configuration.
- [ ] EF Core is configured and migrations run successfully.
- [ ] Each business module owns its database schema/tables and migrations according to the agreed modular boundaries.
- [ ] Database can be recreated from migrations in a clean environment.
- [ ] Cross-module database access is prevented by architecture conventions/tests.
```

------------------------------------------------------------------------

## Attachments

None.

------------------------------------------------------------------------

## Dependencies

-   **Blocked by / related ids:**
    -   `CRM-105` --- ASP.NET Core Modular Monolith Foundation ---
        completed.
    -   `CRM-197` --- Docker Compose & Local Infrastructure ---
        completed.
-   **Depends on code areas or other stories:**
    -   Preserve CRM-105 module boundaries, BuildingBlocks rules,
        Problem Details/API host foundation and `.NET 10 / net10.0`
        baseline.
    -   Reuse CRM-197 root Docker Compose PostgreSQL service and
        existing `POSTGRES_*` environment contract.

### Stories blocked by this story

-   `CRM-202` --- Automated Testing & Architecture Tests
-   `CRM-199` --- Hangfire Background Processing Foundation
-   `CRM-198` --- Domain Events, Integration Events & Transactional
    Outbox
-   `CRM-158` --- Manage Knowledge Base Content Structure
-   `CRM-122` --- Create Customer Profile
-   `CRM-110` --- User Authentication & Session Management

------------------------------------------------------------------------

## Extra notes (optional)

-   This story establishes persistence architecture, not CRM business
    features.
-   Do not create Customers, Leads, Deals, Activities, Users or other
    real business models merely to prove EF Core.
-   The existing `ArchitectureFixture` module from CRM-105 may be used
    as a temporary persistence fixture if a concrete model is required
    to prove schema/migration ownership. It must remain explicitly
    non-business and removable.
-   PostgreSQL `18.6-alpine3.24` and its CRM-197 volume/layout remain
    unchanged unless a genuine compatibility blocker is discovered.
-   Do not add Hangfire, Outbox, domain/integration event
    infrastructure, authentication/session persistence, file storage or
    external integrations.
-   Existing frontend, ADRs, architecture docs, `.claude/`, generated
    `.squad/` files and unrelated repository content must be preserved.

------------------------------------------------------------------------

## Technical hints (optional)

-   Repo/root: `.`
-   Backend: `src/backend/`
-   Target framework: `net10.0`
-   Database: PostgreSQL
-   Local service: root `docker-compose.yml`, service name `postgres`
-   Existing environment keys: `POSTGRES_HOST`, `POSTGRES_PORT`,
    `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`
-   Persistence ORM: EF Core with the PostgreSQL provider.
-   Architecture: Modular Monolith with module-owned persistence.

### Persistence architecture direction

Prefer **one DbContext per module**, not one shared application-wide
DbContext.

Conceptually:

``` text
Module
├── Contracts
└── Implementation
    └── Persistence
        ├── <Module>DbContext
        ├── Entity configurations
        └── Migrations
```

The exact folders/projects should follow the CRM-105 structure and
remain minimal. Do not generate empty persistence projects for future
modules.

Each module DbContext:

-   owns only that module's EF model;
-   uses that module's PostgreSQL schema as its default schema;
-   owns its own migrations;
-   must not expose another module's DbSet/entities;
-   must not directly query another module's private schema/tables.

Do **not** introduce a shared `SquadCrmDbContext` containing all
modules.

### ArchitectureFixture persistence proof

Because CRM-106 must prove migrations/schema ownership before real
business modules exist, the existing `ArchitectureFixture` may receive
the minimum persistence-only model necessary to demonstrate:

-   a module-owned DbContext;
-   a module-owned PostgreSQL schema;
-   a module-owned table;
-   module-owned migrations;
-   clean database recreation.

Any fixture entity/table must be clearly named as architecture/test
scaffolding and must not model a CRM concept.

Do not let the fixture become a permanent business module. Document that
its persistence fixture can be removed when real modules provide
equivalent coverage.

### PostgreSQL schema ownership

-   Use an explicit schema for each module; do not place module tables
    in PostgreSQL `public`.
-   The fixture should use an unmistakable non-business schema such as
    `architecture_fixture` unless the plan identifies a better
    convention.
-   Table/entity naming should follow a consistent convention suitable
    for PostgreSQL.
-   Avoid cross-schema foreign keys between modules as the default
    modular-monolith design.
-   Do not use another module's schema through raw SQL, views, EF
    mappings or direct DbContext access to bypass boundaries.
-   Database-level PostgreSQL roles/permissions per module are **not
    required** in CRM-106 unless the planner finds an existing
    repository requirement; enforcement is primarily
    architectural/application-level for now.

### Connection configuration

CRM-197 already owns the Compose-facing `POSTGRES_*` keys. CRM-106 must
connect ASP.NET Core to those coordinates without introducing a
competing secret/configuration contract.

Prefer a clear configuration adapter/options layer that constructs or
maps the PostgreSQL connection string from environment/configuration
values at the application composition boundary.

Requirements:

-   no committed real password/secret;
-   no hard-coded production connection string;
-   local values remain compatible with `env/backend.env.example`;
-   standard ASP.NET Core configuration precedence remains intact;
-   fail fast with a useful error when required DB configuration is
    missing/invalid;
-   do not log the password or full sensitive connection string.

If a conventional `ConnectionStrings:*` value is useful internally, the
plan must explain how it is derived/mapped from the existing
`POSTGRES_*` operator contract rather than requiring developers to
maintain two competing sets of values.

### EF Core / Npgsql versions

Use EF Core and Npgsql versions compatible with the repo's
`.NET 10 / net10.0` baseline and PostgreSQL 18.

Do not blindly use `latest`.

During planning/implementation:

-   inspect current compatible stable package versions;
-   keep all EF Core packages on a compatible aligned major/version;
-   use the official Npgsql EF Core provider;
-   avoid unnecessary provider/extensions packages;
-   record the selected versions and rationale.

Do not downgrade PostgreSQL solely to fit an arbitrary package choice if
a supported provider version works with PostgreSQL 18.

### Migrations ownership

Each module owns its migration history and migration code.

The plan should choose a deterministic design-time strategy so commands
can target a specific module DbContext without relying on runtime hacks.

Expected properties:

-   migration files live with the owning module
    implementation/persistence area;
-   migration commands explicitly identify the target
    context/project/startup project where needed;
-   migrations set the owning schema explicitly;
-   migrations are source controlled;
-   no single central migrations folder owns all modules;
-   no automatic `Database.Migrate()` on normal application startup
    unless an existing repository rule explicitly requires it.

Prefer **explicit developer/deployment migration commands** over
silently mutating production databases during API startup.

### Migration history

Because multiple module DbContexts share one physical PostgreSQL
database, avoid accidental collisions in EF migration history.

The plan must explicitly decide and document the migration-history
strategy, preferably module-specific history placement/name (for example
within each module's schema) if supported cleanly.

Do not leave this implicit.

### Transactions

-   A module may use normal EF Core transactions within its own
    DbContext/schema.
-   Do not introduce a shared cross-module DbContext just to obtain
    cross-module ACID transactions.
-   Do not implement distributed transactions.
-   Do not implement the Outbox here; CRM-198 owns that.
-   Cross-module workflow consistency belongs to public contracts/events
    and later event/outbox design.

### Applying migrations

Provide a developer workflow that can:

1.  start PostgreSQL via CRM-197;
2.  apply the module's migrations explicitly;
3.  verify the expected schema/table/history exists;
4.  destroy/reset the local DB volume;
5.  recreate the database from migrations alone.

Do not rely on committed init SQL or manual schema creation.

### Architecture enforcement

CRM-106 Acceptance Criteria require cross-module database access to be
prevented by conventions/tests.

Extend the existing architecture verification minimally to cover
persistence boundaries. Tests should prove deterministic dependency
rules such as:

-   another module cannot reference a module's persistence
    implementation;
-   Contracts projects do not depend on EF Core/Npgsql;
-   BuildingBlocks does not depend on module persistence;
-   API host does not directly consume module DbContext/entity internals
    except through the module's intended registration/composition
    surface;
-   forbidden central/shared DbContext patterns are absent;
-   module implementation does not gain a dependency on another module
    implementation/persistence assembly.

Do not pretend static architecture tests can prove every possible SQL
query at runtime. Document what is structurally enforced and what
remains a coding convention until stronger DB-level isolation is
intentionally introduced.

### Integration verification

CRM-106 should include the minimum real PostgreSQL integration
verification needed for its own Acceptance Criteria.

At minimum prove against the CRM-197 PostgreSQL service:

-   backend configuration can create/open a database connection;
-   migrations apply successfully;
-   expected module schema/table/migration-history artifacts exist;
-   a clean database/volume can be recreated using migrations only.

Avoid building the full test-infrastructure strategy owned by CRM-202.

### Naming/conventions

The plan should make an explicit, small decision for PostgreSQL
identifier naming.

Prefer predictable lowercase/snake_case database identifiers unless
existing repo conventions require otherwise. If a naming-conventions
package is proposed, justify the dependency; otherwise explicit EF
mappings/configurations are acceptable for the small fixture.

Do not add a broad package solely to save a few `HasColumnName` calls in
a fixture.

### Health checks

Do not expand `/health` into database readiness in CRM-106 unless the
story/ADR explicitly requires it. CRM-201 owns broader
health/readiness/observability.

A database connectivity check used by CRM-106 integration tests is
sufficient.

### Security / secrets

-   Never commit real DB credentials.
-   Never print passwords in logs/test output.
-   Do not expose DB connection details through API endpoints.
-   Keep local developer defaults clearly non-production.
-   Preserve loopback-only PostgreSQL publishing from CRM-197.

### Documentation expected

Update only the minimum documentation required for developers to use the
persistence foundation, primarily `src/backend/README.md` and root
`README.md` where appropriate.

Document:

-   persistence architecture (`DbContext` per module);
-   schema ownership;
-   environment/config mapping;
-   exact migration commands;
-   how to apply migrations;
-   how to add a future module migration;
-   how to reset/recreate the local database;
-   migration-history strategy;
-   ArchitectureFixture persistence being temporary/removable;
-   what CRM-106 deliberately does not solve (cross-module distributed
    transactions, Outbox, DB roles, production migration automation).

Do not rewrite existing ADRs/architecture documents during
implementation.

### Verification expected from the generated plan

At minimum, include checks for:

-   `dotnet restore`;
-   `dotnet build` with zero errors/warnings under the existing
    warnings-as-errors policy;
-   existing tests;
-   new persistence architecture tests;
-   PostgreSQL starts healthy through CRM-197 Compose;
-   environment configuration successfully opens a PostgreSQL
    connection;
-   EF migrations apply;
-   expected fixture schema/table/module-specific migration history
    exist;
-   clean-volume recreation from migrations succeeds;
-   no tables accidentally land in `public`;
-   no central/shared DbContext is introduced;
-   Contracts/BuildingBlocks remain free of EF Core/Npgsql;
-   no direct cross-module persistence dependencies;
-   no automatic startup migration;
-   no EF/Hangfire/Outbox/Auth scope creep beyond CRM-106;
-   frontend regression remains green if repository-wide verification
    requires it.

------------------------------------------------------------------------

## Out of scope

-   Real CRM business entities/modules.
-   Shared application-wide DbContext.
-   Cross-module direct database access.
-   Cross-module foreign keys as the default integration mechanism.
-   PostgreSQL role-per-module/database permission isolation.
-   Automatic production migration orchestration.
-   `Database.Migrate()` on normal API startup.
-   Distributed transactions.
-   Transactional Outbox/domain/integration event infrastructure
    (`CRM-198`).
-   Hangfire (`CRM-199`).
-   Authentication/session persistence (`CRM-110`).
-   Full automated test infrastructure (`CRM-202`) beyond CRM-106's
    required persistence verification.
-   Full DB readiness/observability pipeline (`CRM-201`).
-   File storage or external integrations.
-   Production deployment infrastructure.