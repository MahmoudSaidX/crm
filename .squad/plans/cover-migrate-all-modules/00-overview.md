# cover-migrate-all-modules — plan overview

Entry point for the **cover-migrate-all-modules** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 23 | [23-story-crm-207-cover-migrate-all-modules.md](23-story-crm-207-cover-migrate-all-modules.md) | Cover all application module migrations in ./scripts/migrate | CRM-207 | None |

## Dependency notes

Single-story feature: extends `scripts/migrate` to discover and apply every application
module's EF Core migrations by scanning for `Persistence/Migrations` directories,
instead of a hardcoded two-module list. No dependency on other in-flight stories.
