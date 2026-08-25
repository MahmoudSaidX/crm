# ADR-002 --- PostgreSQL Persistence & Schema Ownership

**Status:** Accepted baseline.

## Decision

Use PostgreSQL + EF Core with schema-per-module. Modules own migrations
and transactions. SQL Server is not selected.

## Rule

Story plans may refine details but must not silently contradict this
ADR.
