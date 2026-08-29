# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/hangfire-background-processing-foundation/CRM-199/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Hangfire Background Processing Foundation
- **Feature slug (folder under `plans/`):** `hangfire-background-processing-foundation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-199` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** `foundation`
- **Milestone:** `Sprint 0 — Project Setup`
- **Priority:** `Urgent`
- **Estimate:** `3 Points`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
[Sprint 0] Hangfire Background Processing Foundation
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
## User Story

As a developer, I want Hangfire configured for durable background processing so that reminders, SLA checks, outbox delivery and integration jobs have a common execution mechanism.

## Acceptance Criteria

* Hangfire is configured with durable PostgreSQL-backed storage.
* One-off, delayed and recurring jobs can be registered.
* Retry/failure behavior and structured logging are configured.
* Dashboard access is authorization-protected outside local development.
* Job handlers are idempotent and resolve scoped dependencies correctly.
* Background processing publishes pending outbox records written by CRM-198, with retry and idempotency safeguards.
* Concurrency-safe claiming/leasing of pending outbox records is implemented, and any lease/backoff columns it requires are added by additive migration.
* If a store abstraction over the module outbox tables is warranted by a real consumer, this story introduces it. CRM-198 deliberately ships no `IOutboxMessageStore`.

## Business Rules

* Hangfire is the background-job mechanism for the modular monolith.
* Jobs orchestrate application services; business rules do not live in Hangfire-specific classes.
* Jobs must tolerate retries without duplicating business effects.
* Event consumers must be idempotent.
* Outbox delivery is at-least-once; consumers dedupe on the integration event's identity.
* This story defines what "pending" excludes once retries are exhausted (poison-message ceiling).

## Fields Dictionary

No user-facing fields. Job metadata is managed by Hangfire.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
* Hangfire is configured with durable PostgreSQL-backed storage.
* One-off, delayed and recurring jobs can be registered.
* Retry/failure behavior and structured logging are configured.
* Dashboard access is authorization-protected outside local development.
* Job handlers are idempotent and resolve scoped dependencies correctly.
* Background processing publishes pending outbox records written by CRM-198, with retry and idempotency safeguards.
* Concurrency-safe claiming/leasing of pending outbox records is implemented, and any lease/backoff columns it requires are added by additive migration.
* Introduce a module-outbox store abstraction only if the real processor consumer warrants it.

Business Rules:
* Hangfire is the background-job mechanism for the modular monolith.
* Jobs orchestrate application services; business rules do not live in Hangfire-specific classes.
* Jobs must tolerate retries without duplicating business effects.
* Event consumers must be idempotent.
* Outbox delivery is at-least-once; consumers dedupe on the integration event's identity.
* Poison messages are excluded from pending work after the configured retry ceiling.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CRM-198, CRM-197 and CRM-106 (all Done and merged). CRM-199 blocks CRM-201 and CRM-202; do not implement either here.
- **Depends on code areas or other stories:**
  - CRM-198's module-owned `ArchitectureFixture` outbox table and event contracts are the current implementation boundary to extend.
  - CRM-197/CRM-106's existing `POSTGRES_*` configuration and schema-per-module EF Core pattern remain authoritative.
  - `docs/adr/ADR-005-events-outbox.md` requires durable integration through a transactional outbox and idempotent consumers.
  - `docs/adr/ADR-006-background-jobs.md` selects Hangfire and requires business state outside Hangfire with idempotent retries.

## Extra notes (optional)

- CRM-198 intentionally deferred the claim/read-back store, background publisher, retry/backoff/lease mechanism and consumer idempotency to CRM-199.
- Keep Hangfire in the API/infrastructure composition boundary; module business rules remain provider-neutral.
- The current authorization foundation registers authorization services but no authentication scheme (CRM-110). Fail closed: expose the Hangfire dashboard only in Development rather than inventing interim credentials or authentication.
- Treat the retry ceiling and schedule as validated configuration with conservative defaults; they are operational implementation choices, not new product behavior.
- **Open architectural decision:** Linear requires pending records to be "published", but no ADR or current story selects a transport, broker, in-process integration-event dispatcher, or real consumer. Do not mark an outbox row processed through a no-op publisher. The plan must stop before implementation until the publication boundary is selected.

## Technical hints (optional)

- Backend only: `src/backend/`, ASP.NET Core `.NET 10`, EF Core + PostgreSQL.
- Pin `Hangfire.AspNetCore` 1.8.24 and `Hangfire.PostgreSql` 1.21.1.
- Use the existing derived Squad CRM PostgreSQL connection string; Hangfire owns its own storage schema and is execution infrastructure, never business state.
- Additive changes to the module-owned outbox must preserve schema ownership and migrations.

## Out of scope

- CRM-201 OpenTelemetry, metrics, readiness checks or observability pipeline.
- CRM-202 generalized test/CI foundation.
- Reminder, SLA, integration-provider or other future business jobs.
- Authentication/session implementation (CRM-110) or an ad-hoc dashboard credential scheme.
- A generic cross-module job framework, registry or provider catalog.
- Outbox retention/purge policy beyond excluding processed/exhausted rows from pending claims.
