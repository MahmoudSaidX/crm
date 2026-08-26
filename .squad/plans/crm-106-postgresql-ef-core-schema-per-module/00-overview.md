# crm-106-postgresql-ef-core-schema-per-module — plan overview

Entry point for the **crm-106-postgresql-ef-core-schema-per-module** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 05 | `05-story-crm-106-postgresql-ef-core-schema-per-module.md` | PostgreSQL + EF Core + Schema-per-Module | CRM-106 | Story 03 (CRM-105), Story 04 (CRM-197) |

## Dependency notes

- **Depends on** the modular monolith foundation from CRM-105 (Story 03 — solution layout, `IModule` composition, `ArchitectureFixture`, warnings-as-errors, architecture tests) and the local PostgreSQL service from CRM-197 (Story 04 — `docker-compose.yml`, the `POSTGRES_*` operator contract, the `squadcrm-pgdata` volume). Neither is modified: this story consumes the `POSTGRES_*` keys as they are and adds no competing configuration contract.
- **Blocks** CRM-198 (domain/integration events + transactional outbox), CRM-199 (Hangfire), CRM-202 (automated + architecture test infrastructure), CRM-158, CRM-122 and CRM-110. Those stories build on the `DbContext`-per-module pattern established here; none of their infrastructure is added by this story.
- **Shared contract:** the five `POSTGRES_*` keys in `env/backend.env.example` (lines 13–17) stay the **only** operator-facing database configuration. This story derives one Npgsql connection string from them in a new provider-specific adapter, `src/backend/src/Infrastructure/SquadCrm.Infrastructure.Postgres`, and publishes it internally as `ConnectionStrings:SquadCrmPostgres` at composition time. `SquadCrm.BuildingBlocks` stays **provider-neutral** — no EF Core, no Npgsql — and an architecture rule enforces it. EF Core lives only in module implementation projects.
- **Persistence pattern for later modules:** one `DbContext` per module, inside the module implementation project, with its own PostgreSQL schema, its own migrations under `Persistence/Migrations/` and its own migration-history table in that schema. There is deliberately no shared `SquadCrmDbContext` — an architecture rule fails the build if one appears. Runtime composition and the design-time `dotnet ef` factory share one configuration implementation; the factory reads the process environment only.
- **Test responsibility split:** `SquadCrm.ArchitectureTests` stays purely static (dependency direction and `DbContext` placement); all real schema/table/history/`public` claims belong to the new `SquadCrm.Persistence.IntegrationTests`, which requires the CRM-197 PostgreSQL service and fails loudly rather than skipping. CI orchestration and test filtering are deferred to CRM-202.
- Implements `docs/adr/ADR-002-postgresql.md`; it does not amend it. No new ADR required.
