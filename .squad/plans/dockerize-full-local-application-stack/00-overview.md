# dockerize-full-local-application-stack — plan overview

Entry point for the **dockerize-full-local-application-stack** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 21 | `21-story-crm-205-dockerize-full-local-application-stack.md` | Dockerize Full Local Application Stack | CRM-205 | Story 04 (CRM-197), Story 03 (CRM-105), Story 05 (CRM-106) |

## Dependency notes

- **Depends on** [`../crm-197-docker-compose-local-infrastructure/00-overview.md`](../crm-197-docker-compose-local-infrastructure/00-overview.md) (root `docker-compose.yml`, `env/backend.env` contract, `squadcrm-pgdata` volume), the backend baseline from CRM-105 (Story 03, `net10.0`, `global.json`), and the backend's own PostgreSQL wiring from CRM-106 (Story 05, `PostgresConfiguration`/`PostgresOptions`). None of those three stories' files are modified beyond the additive Compose services and the single new `CORS__AllowedOrigins__1` line described in Story 21.
- **Does not block** any other story: this is a leaf developer-experience story, not a foundation other work depends on.
- **Shared contract:** extends CRM-197's `env/backend.env` contract; adds no new configuration keys except `CORS__AllowedOrigins__1`, and no new ports beyond the ones the Angular workspace (`angular.json`) and the backend (`ASPNETCORE_URLS`) already use outside Docker.
- No ADR required — this is local developer tooling, not an architectural decision.
