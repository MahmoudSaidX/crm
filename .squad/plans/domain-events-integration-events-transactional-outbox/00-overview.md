# domain-events-integration-events-transactional-outbox — plan overview

Entry point for the **domain-events-integration-events-transactional-outbox** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 07 | `07-story-crm-198-domain-events-integration-events-transactional-outbox.md` | Domain Events, Integration Events & Transactional Outbox | CRM-198 | Story 05 (CRM-106), Story 06 (CRM-204) |

## Dependency notes

- **Depends on** Story 05 (CRM-106 — PostgreSQL + EF Core + Schema-per-Module: the per-module `DbContext`/schema/migration pattern this story's outbox table reuses) and Story 06 (CRM-204 — Shared API/Validation/Security Foundation: `CorrelationIdMiddleware`, whose `HttpContext.TraceIdentifier` becomes `OutboxMessage.CorrelationId` via a new `ICorrelationIdAccessor`). Neither is modified.
- **Blocks** CRM-199 (Hangfire Background Processing Foundation — owns the claim/read-back abstraction over the outbox table, background publishing, retry and idempotent processing; CRM-198 deliberately builds none of that, per user ruling), CRM-202 (Automated Testing & Architecture Tests), and 14 further backlog stories (CRM-192, CRM-193, CRM-186, CRM-167, CRM-164, CRM-155, CRM-154, CRM-152, CRM-150, CRM-147, CRM-144, CRM-139, CRM-137, CRM-129) that will raise real domain/integration events once real business modules exist. None of their content is implemented here.
- **Scope boundary — CLOSED (user ruling, recorded in Linear):** AC 4 (background processing) reassigned to CRM-199 in full; AC 5 (observability) reassigned to CRM-201 in full. CRM-198 owns exactly: domain events raised in-module, explicit integration-event contracts, and the transactional outbox write path (outbox table owned by the writing module, in that module's own schema). `IOutboxMessageStore` is deferred to CRM-199 — not built here.
- **Contracts home (user ruling):** a new dependency-free project `SquadCrm.BuildingBlocks.Abstractions` holds only `IDomainEvent`/`IIntegrationEvent`; module `*.Contracts` projects reference only that project, never the ASP.NET-Core-bearing `SquadCrm.BuildingBlocks`. `OutboxMessage` is a persistence implementation detail owned by each module, not a shared type.
- Implements `docs/adr/ADR-005-events-outbox.md`; touches `docs/adr/ADR-006-background-jobs.md` and `docs/adr/ADR-008-observability.md` only at the boundary (does not amend either). No new ADR required.
