# crm-197-docker-compose-local-infrastructure — plan overview

Entry point for the **crm-197-docker-compose-local-infrastructure** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 04 | `04-story-crm-197-docker-compose-local-infrastructure.md` | Docker Compose & Local Infrastructure | CRM-197 | Story 01 (CRM-107), Story 03 (CRM-105) |

## Dependency notes

- **Depends on** the repository/environment conventions from CRM-107 (Story 01, `env/backend.env.example`, `.gitignore`, root `README.md`) and the backend configuration contract from CRM-105 (Story 03). Neither is modified beyond one comment in `env/backend.env.example` and the README rows CRM-107 reserved for this story.
- **Blocks** CRM-106 (PostgreSQL + EF Core + schema-per-module), CRM-199 (Hangfire), CRM-201 (OpenTelemetry / health checks) and CRM-202 (automated + architecture tests). Those stories consume the Compose coordinates documented here; this story deliberately adds no EF Core, migrations, schemas or application persistence code.
- **Shared contract:** the `POSTGRES_*` keys in `env/backend.env.example` (~lines 8–13). CRM-106 owns whatever connection string is eventually bound; the mapping is documented in the root `README.md` so both stay compatible.
- Implements `docs/adr/ADR-002-postgresql.md`; it does not amend it. No new ADR required.
