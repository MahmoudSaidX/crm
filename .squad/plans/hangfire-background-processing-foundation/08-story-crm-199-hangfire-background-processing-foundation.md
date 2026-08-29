# Story 08 — Hangfire Background Processing Foundation (Story: CRM-199)

## Prerequisites

- Story 07 completed: [CRM-198 — Domain Events, Integration Events & Transactional Outbox](../domain-events-integration-events-transactional-outbox/07-story-crm-198-domain-events-integration-events-transactional-outbox.md). Reuse its module-owned outbox and explicit integration-event boundary; do not move outbox state into Hangfire or a shared DbContext.
- CRM-197 and CRM-106 are Done and merged. Reuse the existing `POSTGRES_*` configuration adapter and schema-per-module migration pattern.
- `docs/adr/ADR-005-events-outbox.md` and `docs/adr/ADR-006-background-jobs.md` are binding: delivery is durable and at-least-once, consumers are idempotent, Hangfire is execution infrastructure, and business state remains in module/application models.
- **Approved publication boundary:** use the minimum in-process dispatcher and
  ArchitectureFixture consumer with a durable receipt keyed by integration-event
  `EventId`. This proves successful at-least-once processing without selecting
  or abstracting an external transport.

---

## Story Goal

Configure Hangfire with durable PostgreSQL storage and provide the minimum recurring outbox-delivery path deferred by CRM-198: atomically claim eligible module-owned rows, publish them through a real boundary, mark success, and record bounded retry state on failure. Prove one-off, delayed and recurring registration and scoped dependency resolution without adding future reminder/SLA jobs, CRM-201 observability, or a generic job framework.

The Hangfire dashboard is mapped only in Development until CRM-110 supplies real authentication. Absence outside Development is fail-closed and avoids inventing an interim credential scheme.

---

## Context — Read These Files First

1. `AGENTS.md` — follow Story Execution Safety, YAGNI, module boundaries and publication restrictions.
2. `docs/adr/ADR-005-events-outbox.md` — preserve the transactional-outbox and idempotent-consumer decision.
3. `docs/adr/ADR-006-background-jobs.md` — use Hangfire only for execution; keep business state outside Hangfire.
4. `src/backend/src/Api/SquadCrm.Api/Program.cs` — extend the composition root after `AddSquadCrmPostgres()` and map the Development-only dashboard without adding authentication.
5. `src/backend/src/Infrastructure/SquadCrm.Infrastructure.Postgres/PostgresConfiguration.cs` — reuse `GetSquadCrmPostgresConnectionString()`; do not add a second operator-facing database secret.
6. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/OutboxMessage.cs` and `OutboxMessageConfiguration.cs` — extend the module-owned state and filtered pending index; do not create a shared outbox entity.
7. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureDbContext.cs` and `ArchitectureFixtureModule.cs` — keep reads/writes inside the owning module and register the real processor/store as module services.
8. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/OutboxTransactionalWriteTests.cs` and `PostgresTestDatabase.cs` — follow the real-PostgreSQL integration-test pattern.
9. `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — remove only the deliberate solution-wide Hangfire prohibition and replace it with a narrow placement rule; keep all unrelated forbidden packages.

---

## Product rules (from story)

- Delivery is at-least-once. The integration event `EventId` is the consumer dedupe key.
- A row is pending only when unprocessed, below the retry ceiling, not currently leased, and eligible under backoff.
- A failed attempt increments retry state and releases the lease. Exhausted rows are not reclaimed automatically.
- Hangfire jobs orchestrate module/application services; they contain no business rules and do not access another module's tables.
- Failure text is truncated and sanitized. Payloads, secrets and connection strings are never logged or copied into diagnostic fields.

---

## Approved decision — publication boundary

The user approved Option B. Implement a fixture-specific in-process dispatcher
that recognizes only `ArchitectureFixtureProbeRecordedIntegrationEvent` and
invokes one ArchitectureFixture consumer. Persist a module-owned receipt keyed
by `EventId` in the same transaction as the fixture consumer's durable effect.
On redelivery, the existing receipt makes consumption a no-op. Mark the outbox
row processed only after that consumer transaction commits.

This is executable proof, not the final external messaging architecture. Add no
external broker, transport/provider abstraction, assembly scanning, generic
handler registry, reusable job framework or general-purpose event bus. Future
external transport selection remains deferred to a real integration story.

---

## Backend Tasks

### 1 — Add Hangfire packages and validated operational configuration

**File: `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj`**

- Add `Hangfire.AspNetCore` version `1.8.24` and `Hangfire.PostgreSql` version `1.21.1`.

**Create file: `src/backend/src/Api/SquadCrm.Api/BackgroundProcessingOptions.cs`**

- Bind `BackgroundProcessing` with conservative validated defaults: recurring outbox interval, batch size, lease duration, retry ceiling and backoff.
- Reject non-positive durations/batch sizes, a retry ceiling below one, and a lease duration that is not longer than the expected single-attempt timeout.
- Store no secrets. PostgreSQL still comes exclusively from `PostgresConfiguration`.

**Files: `src/backend/src/Api/SquadCrm.Api/appsettings.json`, `env/backend.env.example`, `src/backend/README.md`**

- Document only non-secret tuning keys and their defaults.

### 2 — Configure Hangfire as host execution infrastructure

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`**

- Configure Hangfire PostgreSQL storage with the already-derived Squad CRM connection string and a dedicated Hangfire schema/table prefix supported by `Hangfire.PostgreSql`.
- Register the Hangfire server and a small registration service that exposes one-off, delayed and recurring scheduling without wrapping Hangfire business APIs in a generic framework.
- Register the recurring outbox job after application construction using a stable job identifier.
- Map the Hangfire dashboard only inside `app.Environment.IsDevelopment()`. Do not map it in non-Development environments until CRM-110 provides authentication.
- Configure bounded automatic retry behavior and structured framework logging; do not add OpenTelemetry, metrics, readiness probes or a logging pipeline.

### 3 — Add module-owned lease/retry state by additive migration

**Files: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/OutboxMessage.cs`, `OutboxMessageConfiguration.cs`**

- Add nullable `LeaseId`, `LeasedUntilUtc` and `NextAttemptAtUtc` state plus mutation methods that enforce claim ownership.
- Add module-owned operations for claim, success and failure. Success sets `ProcessedAtUtc` and clears lease/error state. Failure increments `RetryCount`, stores a sanitized/truncated error, sets backoff eligibility when below the ceiling, and clears the lease.
- Replace the pending index with a filtered/composite index supporting unprocessed, below-ceiling and eligible-at ordering. Keep every column in `architecture_fixture.outbox_message`.

**Create migration under `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/Migrations/`**

- Add only the new nullable lease/backoff columns and supporting index changes. Existing CRM-198 rows remain eligible after migration.

### 4 — Implement concurrency-safe module-owned claiming

**Create files under `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/BackgroundProcessing/`**

- Add a module-internal store consumed by the real processor. Do not expose another module's `DbContext` or outbox entity.
- Claim a bounded batch in a transaction using PostgreSQL row locking with `FOR UPDATE SKIP LOCKED`, ordered by occurrence time and ID.
- Assign one lease ID and expiry to the claimed rows before committing. Competing workers must receive disjoint batches.
- Success/failure updates must match both message ID and lease ID so an expired worker cannot overwrite a newer claim.
- Treat expired leases as eligible. Exclude processed rows, exhausted rows and rows whose backoff is in the future.

### 5 — Implement the selected publication boundary and outbox processor

**Create files under `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/BackgroundProcessing/`**

- Implement the approved fixture-specific in-process dispatcher and consumer.
- Deserialize only the explicit versioned type `architecture-fixture.probe-recorded.v1`. Unknown types fail safely and enter bounded retry; never silently mark them processed.
- Process each claimed message in its own DI scope/DbContext lifetime. Mark success only after publication/dispatch completes.
- Preserve the CRM-198 payload and event ID. Do not log payload content.
- Make the fixture consumer idempotent with a durable module-owned receipt keyed by event ID and prove replay is a no-op.

### 6 — Add the Hangfire orchestration job

**Create file: `src/backend/src/Api/SquadCrm.Api/OutboxDeliveryJob.cs`**

- Keep the Hangfire-facing class thin: create/use scoped services, invoke one processor batch and return.
- Add no business logic, module table access, reminder/SLA behavior or provider registry.
- Allow Hangfire retry to re-run safely; store-level leases and consumer dedupe remain authoritative.

### 7 — Narrow architecture rules and documentation

**File: `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`**

- Remove `Hangfire` from the solution-wide forbidden-prefix list because CRM-199 legitimately adds it.
- Add a focused rule that only the API/background-execution composition boundary may reference Hangfire. Module contracts, BuildingBlocks abstractions and module business/persistence code remain Hangfire-free.

**File: `src/backend/README.md`**

- Document registration, Development-only dashboard behavior, retry/lease semantics, poison-message exclusion and the selected publication boundary.
- State explicitly that CRM-201 owns telemetry/health observability and future stories own business jobs.

---

## Edge Cases & Failure Modes

- Two workers claim simultaneously: `SKIP LOCKED` and committed lease assignment produce disjoint batches.
- A worker dies after claim: lease expiry makes the row eligible again; delivery remains at-least-once.
- A stale worker completes after lease expiry: lease-ID matching prevents it from changing the newer owner's state.
- Publication throws: increment retry count, store only sanitized bounded diagnostics, calculate backoff and release the lease.
- Retry ceiling reached: the row is exhausted and excluded from pending claims; no deletion or automatic reset occurs.
- Unknown event type or malformed payload: fail the attempt and retain the durable row; never mark it processed.
- Duplicate delivery: the selected consumer boundary dedupes by integration-event ID before applying an effect.
- Non-Development host: no Hangfire dashboard endpoint exists.
- PostgreSQL unavailable: Hangfire and outbox processing fail without falling back to in-memory execution/storage.

---

## Test Plan

1. Add API tests proving Hangfire services/server registration, one-off/delayed/recurring scheduling, scoped job activation and Development-only dashboard mapping.
2. Add configuration tests for valid defaults and every rejected background-processing option boundary.
3. Add real-PostgreSQL integration tests for additive migration, disjoint concurrent claims, expired lease recovery, stale lease rejection, successful processing and bounded retry/backoff/exhaustion.
4. Add publication tests for unknown types, malformed payloads and failure without premature `ProcessedAtUtc`.
5. Add idempotency tests that deliver the same event ID twice and assert one consumer effect/receipt.
6. Update architecture tests to permit Hangfire only in the execution composition boundary and keep contracts/module persistence free of Hangfire references.

---

## Migration / Rollback

- Apply the module-owned additive migration before enabling the recurring processor. Existing rows receive null lease/backoff fields and remain pending.
- A half-applied deployment must not start a processor against the old schema. Deployment order is migration, application, then server activation.
- Rollback stops the Hangfire server first. Do not drop durable outbox rows or Hangfire storage automatically.

---

## Verification Steps

1. **Backend formats:** from `src/backend`, run `dotnet format SquadCrm.sln --verify-no-changes`.
2. **Backend builds:** from `src/backend`, run `dotnet build SquadCrm.sln --no-restore`.
3. **Backend tests:** from `src/backend`, run `dotnet test SquadCrm.sln --no-build` with the repository PostgreSQL environment loaded.
4. **Migration:** run `dotnet ef database update` for `ArchitectureFixtureDbContext`, then verify no pending model changes and run the persistence integration tests.
5. **Regression/scope:** inspect `git diff --check`, `git diff`, and `git status --short`; verify no CRM-201 telemetry, future business jobs, authentication implementation or cross-module persistence access was added.

---

## Done Criteria

- [ ] Hangfire uses durable PostgreSQL storage and supports one-off, delayed and recurring registration.
- [ ] Retry/failure behavior is bounded and structured without CRM-201 observability scope.
- [ ] The dashboard is fail-closed outside Development.
- [ ] Jobs resolve scoped dependencies and contain orchestration only.
- [ ] CRM-198 rows are claimed concurrently without duplicate ownership and published at least once.
- [ ] Successful rows are marked processed only after real publication/dispatch.
- [ ] Failures back off, exhaust at the configured ceiling and never leak sensitive payloads.
- [ ] Consumer behavior is idempotent by integration-event ID.
- [ ] Migrations remain module-owned and reproducible.
- [ ] Architecture, security, data-integrity and downstream-story boundaries pass review.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 09.**
