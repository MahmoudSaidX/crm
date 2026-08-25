# Data Ownership

PostgreSQL + EF Core. Prefer schema-per-module. Each module owns
migrations and private persistence. No direct cross-module
DbContext/table access. Cross-module reads use explicit contracts/read
models; durable reactions use integration events/outbox. Reporting uses
projections. Provider payloads must be normalized rather than becoming
core domain models by accident.
