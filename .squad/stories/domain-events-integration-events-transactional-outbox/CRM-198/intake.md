# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/domain-events-integration-events-transactional-outbox/CRM-198/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Domain Events, Integration Events & Transactional Outbox
- **Feature slug (folder under `plans/`):** `domain-events-integration-events-transactional-outbox`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-198` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Story`
- **Status:** `Backlog` (Ready to plan)
- **Assignee:** ``
- **Labels:** `foundation`
- **Milestone:** `Sprint 0 — Project Setup`
- **Priority:** `Urgent`
- **Estimate:** `5 points`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
[Sprint 0] Domain Events, Integration Events & Transactional Outbox
```

---

## Description

```
## User Story

As a developer, I want reliable domain/integration event foundations with a transactional outbox so that module workflows and external side effects are decoupled without losing events.

## Acceptance Criteria (REVISED — user-approved, recorded in Linear)

* Domain events can be raised inside a module.
* Cross-module/external integration events use explicit contracts.
* Integration events are persisted transactionally with business changes through an outbox.
* The outbox table is owned by the module that writes it, in that module's own schema.

REASSIGNED OUT of CRM-198 (Linear):
* "Background processing publishes pending outbox records with retry and idempotency safeguards" → CRM-199.
* "Processing status/failures are observable" → CRM-201.

## Business Rules (REVISED — user-approved, recorded in Linear)

* Domain events are internal to their owning module unless translated to an integration event.
* Cross-module side effects must not require direct table access.
* A consuming module never reads the producing module's outbox table.
* Business transaction and outbox persistence must succeed/fail together.

REASSIGNED OUT of CRM-198 (Linear):
* "Event consumers must be idempotent" → CRM-199.

## Fields Dictionary

OutboxMessage: Id (UUID), Type (event contract name), Payload (serialized event), OccurredAtUtc (timestamp), ProcessedAtUtc (nullable timestamp), RetryCount (integer), Error (nullable diagnostic text), CorrelationId (trace identifier). CRM-198 writes ProcessedAtUtc/RetryCount/Error only as null/0/null — a future story populates them.
```

---

## Acceptance criteria

```
* Domain events can be raised inside a module.
* Cross-module/external integration events use explicit contracts.
* Integration events are persisted transactionally with business changes through an outbox.
* The outbox table is owned by the module that writes it, in that module's own schema.

Business Rules:
* Domain events are internal to their owning module unless translated to an integration event.
* Cross-module side effects must not require direct table access.
* A consuming module never reads the producing module's outbox table.
* Business transaction and outbox persistence must succeed/fail together.

Fields Dictionary:
OutboxMessage: Id (UUID), Type (event contract name), Payload (serialized event), OccurredAtUtc (timestamp), ProcessedAtUtc (nullable timestamp), RetryCount (integer), Error (nullable diagnostic text), CorrelationId (trace identifier).
```

## User rulings (binding, recorded in Linear — 2026-08-26)

1. **Scope** — CRM-198 owns ONLY the transactional event/outbox write foundation (domain event → same `SaveChanges` transaction → integration event/outbox record → module-owned outbox table). AC 4 (background publishing/retry/idempotency) → CRM-199. AC 5 (observability) → CRM-201. Business Rule "Event consumers must be idempotent" → CRM-199. `IOutboxMessageStore` is DEFERRED to CRM-199 — no current consumer requires it; deleted from this story entirely, along with `EfOutboxMessageStore` and their tests.
2. **Contracts home** — new dependency-free project `SquadCrm.BuildingBlocks.Abstractions`, containing ONLY `IDomainEvent`/`IIntegrationEvent`. No `OutboxMessage`, no store interface, no publisher/retry/scheduler/observability/provider abstraction in it. `OutboxMessage` stays a persistence implementation detail owned by the module that owns its outbox table. No `FrameworkReference`, no package references, no project references in Abstractions. Module `*.Contracts` projects reference ONLY `SquadCrm.BuildingBlocks.Abstractions`. Architecture tests protect this. `SquadCrmAssemblies.All` registers the new assembly. The Contracts csproj comment claiming "NO project references" is updated.
3. **YAGNI** — anything not required to prove the four-step chain above is deferred to its owning downstream story.

Folded blocking/non-blocking review findings (see the revised plan file's Decisions table and task list for the authoritative mapping): B1 (SaveChangesInterceptor, not a SaveChangesAsync override), B2 (ICorrelationIdAccessor, host-registered), B4 (versioned contract-name constant), B5 (`EventId` on `IIntegrationEvent`), B6 (`Ignore(p => p.DomainEvents)`), N2/N3/N5/N6/N7/N8/N9/N10/N11/N14, and the YAGNI trims (delete `OutboxMessage.IsPending`, delete caller-supplied-clock overloads, `ClearDomainEvents()` non-public where achievable).

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

---

## Dependencies

- **Blocked by / related ids (all Done):** CRM-204 (Shared API/Validation/Security Foundation), CRM-106 (PostgreSQL + EF Core + Schema-per-Module), CRM-105 (ASP.NET Core Modular Monolith Foundation)
- **Blocks (16, all Backlog — do not implement any of these here):** CRM-199 (Hangfire Background Processing Foundation), CRM-202 (Automated Testing & Architecture Tests), CRM-192, CRM-193, CRM-186, CRM-167, CRM-164, CRM-155, CRM-154, CRM-152, CRM-150, CRM-147, CRM-144, CRM-139, CRM-137, CRM-129.
- **Depends on code areas:**
  - `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/` — provider-neutral cross-cutting home (Modules, Errors, Http, Security, Validation, Correlation already exist there); any generic outbox/event abstraction that is not persistence-specific belongs here per the established pattern.
  - `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/` — the only existing module-owned `DbContext`/schema/migration pattern in the repo (`ArchitectureFixtureDbContext`, `ArchitectureFixtureDbContextFactory`, `ArchitectureFixtureDbContextOptions`, `ArchitectureFixtureSchema`, `IEntityTypeConfiguration` per entity, explicit lowercase snake_case column mapping, own migrations-history table inside the module's own schema). CRM-198's outbox persistence must follow this same pattern per module, not introduce a shared context.
  - `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — `ForbiddenAssemblyPrefixes` (line 40) currently forbids `Hangfire`, `MediatR`, `FluentValidation`, `Microsoft.AspNetCore.Authentication.`, `Swashbuckle.`, `Scalar.`, `NSwag.` for every assembly (production and test). The doc comment on that list (lines 24-38) explicitly names CRM-198 as the story that "must update this list when [it] legitimately introduce[s] one of these" — but Hangfire itself belongs to CRM-199, not this story. `MediatR` is also currently forbidden, so an in-process event dispatch mechanism must not depend on it unless the plan explicitly justifies lifting that ban (escalate rather than assume).
  - `docs/api-conventions.md` (CRM-204) — shared success/error/pagination/validation/security contract; correlation-id handling (`CorrelationIdMiddleware`) already exists in BuildingBlocks and issues a `CorrelationId` per request — the OutboxMessage's `CorrelationId` field should reuse that existing correlation mechanism rather than invent a new one.
  - `src/backend/src/Infrastructure/SquadCrm.Infrastructure.Postgres/` — Postgres configuration adapter (env-var driven connection string, ADO Npgsql only, no EF Core). Module DbContexts consume this for connection strings; outbox tables live inside each module's own schema via this same adapter, not a new shared adapter.

## Extra notes (optional)

- **Scope tension — RESOLVED by user ruling (see "User rulings" above), no longer open.** AC 4 (background processing) and AC 5 (observability) are formally reassigned to CRM-199 and CRM-201 respectively. CRM-198 owns exactly: outbox table + transactional write path (domain event → same-transaction integration event/outbox row). No poll-able/claimable contract (`IOutboxMessageStore`) is built in this story — deferred to CRM-199 per the YAGNI ruling.
- **ADR-005 (Events & Transactional Outbox):** "Separate domain events from versionable integration events; use transactional outbox and idempotent consumers for durable async integration." Story plans may refine details but must not silently contradict this ADR.
- **ADR-006 (Background Processing):** "Use Hangfire for background/delayed/recurring execution. Business state stays in domain/application models; retries are idempotent." Confirms Hangfire itself is out of scope for CRM-198 (it is CRM-199's story), but "retries are idempotent" is a constraint CRM-198's outbox data model must support regardless of which story wires the scheduler.
- **YAGNI constraint from the controller:** no abstraction, extension point, provider interface, registry, generic framework or configuration mechanism unless it has (a) a requirement in this story, (b) a current consumer, (c) an existing ADR requiring it, or (d) evidence that changing it later is a significant breaking change. Prefer extending the established module/persistence pattern (as seen in ArchitectureFixture) over inventing a new one.
- Do not implement CRM-199 (Hangfire) or CRM-201 (OpenTelemetry/logging/health checks) content in this story. Non-goals stay unimplemented.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `csharp` (backend: ASP.NET Core modular monolith, `.NET 10`, EF Core + PostgreSQL, schema-per-module — see `src/backend/README.md`).
- Existing conventions to extend: `SquadCrm.BuildingBlocks.Modules.IModule` / `ModuleRegistrar` for module registration; `SquadCrm.BuildingBlocks.Correlation.CorrelationIdMiddleware` for the `CorrelationId` field; per-module `Persistence/` folder with its own `DbContext`, `DbContextFactory` (design-time), `DbContextOptions` (Npgsql + own migrations-history table inside own schema), `Schema` constants class, and `IEntityTypeConfiguration<T>` per entity, exactly as `ArchitectureFixtureDbContext` demonstrates.
- Relevant ADRs: `docs/adr/ADR-001-modular-monolith.md`, `docs/adr/ADR-002-postgresql.md`, `docs/adr/ADR-005-events-outbox.md` (primary), `docs/adr/ADR-006-background-jobs.md` (boundary only — Hangfire itself is CRM-199), `docs/adr/ADR-008-observability.md` (boundary only — CRM-201), `docs/adr/ADR-011-testing.md`.
- Test layout to extend: `src/backend/tests/SquadCrm.ArchitectureTests` (static rules, no DB), `src/backend/tests/SquadCrm.Api.Tests` (no DB), `src/backend/tests/SquadCrm.Persistence.IntegrationTests` (needs real PostgreSQL via `docker compose up -d` and `env/backend.env`).

## Out of scope

- Hangfire installation, recurring-job scheduling/wiring, or any other CRM-199 background-execution-infrastructure content.
- `IOutboxMessageStore` (or any claim/read-back/poll-able abstraction) and its implementation — deferred to CRM-199 (user ruling 1).
- Background publishing, retry, backoff, claiming/leasing of outbox rows (CRM-199).
- OpenTelemetry, structured logging pipeline, health-check endpoints, or any processing-status observability surface (CRM-201).
- Authentication schemes (CRM-110) and any business-module event producers/consumers — this is Sprint 0 foundation only; no business module exists yet to raise a real domain event.
- Any new shared/generic abstraction, provider interface or registry beyond what the revised AC 1-4 and the Fields Dictionary require (YAGNI Gate, user ruling 3).
