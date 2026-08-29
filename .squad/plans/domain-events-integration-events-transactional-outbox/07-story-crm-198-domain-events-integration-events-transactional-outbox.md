# Story 07 — Domain Events, Integration Events & Transactional Outbox (Story: CRM-198)

> **REVISED IN PLACE per user-approved rulings (see intake.md "User rulings" section).**
> CRM-198 is now scoped to the transactional write path only:
> Domain Event → same `SaveChanges` transaction → Integration Event / serialized
> outbox record → module-owned outbox table. **STOP THERE.** No publisher, no
> scheduler, no retry loop, no claim/lease mechanism, no observability wiring,
> no `IOutboxMessageStore` abstraction. Those are CRM-199 (background
> publishing/retry/idempotent processing) and CRM-201 (processing status/failure
> observability).

## Prerequisites

- **Story 05 completed (CRM-106 — PostgreSQL + EF Core + Schema-per-Module):** one `DbContext` per module, in the module implementation project's `Persistence/` folder, its own PostgreSQL schema, its own migrations, its own migrations-history table inside that schema, a design-time `IDesignTimeDbContextFactory`, and `SquadCrm.Persistence.IntegrationTests` requiring a real, running PostgreSQL (never skips). See [`../crm-106-postgresql-ef-core-schema-per-module/05-story-crm-106-postgresql-ef-core-schema-per-module.md`](../crm-106-postgresql-ef-core-schema-per-module/05-story-crm-106-postgresql-ef-core-schema-per-module.md). This story follows the exact same per-module persistence pattern for the outbox table; it does not invent a new one.
- **Story 06 completed (CRM-204 — Shared API/Validation/Security Foundation):** RFC 9457 Problem Details, `CorrelationIdMiddleware` (promotes a sanitised `X-Correlation-Id` to `HttpContext.TraceIdentifier`, `HeaderName`/`MaxLength = 128` at `CorrelationIdMiddleware.cs:17-20`), `PagedResult<T>`/`ValidationEndpointFilter<T>`. See [`../shared-api-validation-security-foundation/06-story-shared-api-validation-security-foundation.md`](../shared-api-validation-security-foundation/06-story-shared-api-validation-security-foundation.md). This story's `OutboxMessage.CorrelationId` is populated from `HttpContext.TraceIdentifier` via a new small accessor abstraction (Task 3) — it does not invent a second correlation mechanism.
- **`docs/adr/ADR-005-events-outbox.md` is a binding read-only input** — "Separate domain events from versionable integration events; use transactional outbox and idempotent consumers for durable async integration." This story implements the write half of it; **do not amend the ADR.**
- **`docs/adr/ADR-006-background-jobs.md` is a binding read-only input** — Hangfire itself is **CRM-199's** scope. This story adds no Hangfire package, no publisher, no scheduler, no retry loop, no claim/lease mechanism, no `IHostedService`, no `PeriodicTimer`.
- **`docs/adr/ADR-008-observability.md` is a binding read-only input** — structured logging / OpenTelemetry / health checks are **CRM-201's** scope. This story adds no logging pipeline, no metrics, no health-check probe, no processing-status query surface.
- **Scope boundary is now CLOSED, not open** — the user has formally reassigned AC 4 (background publishing/retry/idempotent processing) to CRM-199 and AC 5 (processing status/failure observability) to CRM-201 in Linear. This plan does not re-litigate that split.
- Coordinate with the owners of CRM-199 (Hangfire Background Processing Foundation) and CRM-202 (Automated Testing & Architecture Tests) — both will consume the outbox table this story adds, once CRM-199 defines its own claim/store abstraction. **Implement neither here.**
- Local PostgreSQL must be running for the integration verification: `docker compose up -d` from the repository root, then `set -a && . env/backend.env && set +a` from `src/backend/`.

---

## Story Goal

Give every module a proven way to (1) raise a domain event inside its own boundary, (2) translate a domain event into an explicit, versionable integration-event contract when another module or an external consumer needs to know, and (3) persist that integration event **in the same database transaction** as the business change that caused it, via a per-module transactional outbox table — so an integration event is never lost and never observed before its causing business change has committed. Nothing about *reading* the outbox back out is this story's concern.

1. A new, dependency-free project `SquadCrm.BuildingBlocks.Abstractions` gains exactly two provider-neutral marker contracts: `IDomainEvent` and `IIntegrationEvent`. Nothing else lives here — no `OutboxMessage`, no store interface, no publisher/retry/scheduler/observability/provider abstraction.
2. `SquadCrm.BuildingBlocks` (the existing ASP.NET-bearing project) gains a small `HasDomainEvents` base (`Events/` folder) that any module's entity can opt into to raise domain events, referencing `IDomainEvent` from the new Abstractions project — proven by extending the existing `ArchitectureFixture` fixture (`PersistenceProbe`), not by inventing a real business entity.
3. `SquadCrm.BuildingBlocks.Correlation` gains a small `ICorrelationIdAccessor` abstraction, registered in the **host** composition root, so a module's persistence layer never depends on `HttpContext` directly.
4. `OutboxMessage` is a plain C# class, matching the Fields Dictionary, living as a **persistence implementation detail inside the ArchitectureFixture module** (`Persistence/OutboxMessage.cs`) — not in `BuildingBlocks`, not in `Contracts`. `ArchitectureFixtureDbContext` maps it to its own `outbox_message` table inside its own `architecture_fixture` schema (its own `IEntityTypeConfiguration<OutboxMessage>`, exactly like `PersistenceProbeConfiguration`).
5. An EF Core `ISaveChangesInterceptor` (not a `SaveChangesAsync` override) drains any domain events raised on tracked entities that derive from `HasDomainEvents`, translates the demonstration one into an `ArchitectureFixtureProbeRecordedIntegrationEvent` (new type in the `.Contracts` project, referencing only `SquadCrm.BuildingBlocks.Abstractions`), and adds a corresponding `OutboxMessage` row to the **same** `DbContext`/**same** `SaveChanges` call — proving atomicity through EF Core's single-transaction guarantee, with no `TransactionScope` and no two-phase commit. The interceptor is registered on every `SaveChanges`/`SaveChangesAsync` overload, including the design-time factory path.
6. Architecture tests protect the new dependency boundary: `SquadCrm.BuildingBlocks.Abstractions` has no project/framework/package references; module `*.Contracts` assemblies must not depend on `SquadCrm.BuildingBlocks` (the ASP.NET-bearing one). `MediatR` and `Hangfire` stay forbidden — this story introduces neither.

**Explicitly out of scope (deferred, not implemented):** `IOutboxMessageStore` and any claim/read-back abstraction (CRM-199 — no current consumer requires it); Hangfire installation or any recurring/background job (CRM-199); background publishing, retry, backoff, claiming/leasing (CRM-199); OpenTelemetry, structured logging, metrics, health-check probes, processing-status observability (CRM-201); any real business module or business domain event (none exists yet — Sprint 0 has no business module); a generic/reusable cross-module event-dispatch bus, publish/subscribe abstraction, or message broker (no current second consumer exists — YAGNI); authentication (CRM-110); consumer-side idempotency-key storage for a second module (documented as a contract only — `IIntegrationEvent.EventId` is the dedupe key a future consumer must track).

---

## Context — Read These Files First

1. `.squad/stories/domain-events-integration-events-transactional-outbox/CRM-198/intake.md` — the full intake, including the **User rulings** section recorded from the user's Linear decision. That section is a hard constraint, not a suggestion.
2. `docs/adr/ADR-005-events-outbox.md` and `docs/adr/ADR-006-background-jobs.md` — binding, read-only.
3. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureDbContext.cs` — **lines 1–37** (whole file). It uses a **primary constructor** (`ArchitectureFixtureDbContext(DbContextOptions<ArchitectureFixtureDbContext> options) : DbContext(options)`) — do not add a second constructor overload; the interceptor is supplied through `DbContextOptionsBuilder.AddInterceptors(...)`, not through the constructor (N14).
4. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/PersistenceProbeConfiguration.cs` — **whole file (37 lines)**. Copy its exact style — explicit lowercase snake_case `HasColumnName`, `HasMaxLength`, `HasColumnType("timestamptz")`, no navigation/FK — for the new `OutboxMessageConfiguration`. Adding `builder.Ignore(p => p.DomainEvents)` here is required (B6) once `PersistenceProbe` gains `DomainEvents` — otherwise EF's model build fails trying to map an `IReadOnlyCollection<IDomainEvent>`.
5. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureSchema.cs` — **lines 1–27** (whole file). Add `public const string OutboxTable = "outbox_message";` alongside `ProbeTable`.
6. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/PersistenceProbe.cs` — **whole file (19 lines)**. Changes from a plain POCO to a `HasDomainEvents`-derived type.
7. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureDbContextFactory.cs` and `ArchitectureFixtureDbContextOptions.cs` — **whole files** (both short). `ArchitectureFixtureDbContextOptions.Apply` is the **single** place the interceptor is wired in (Task 4) — both the runtime `AddDbContext` path and the design-time factory path call it, so they cannot diverge (B1).
8. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/ModuleInfo.cs` — **whole file (8 lines)**. Add the new `ArchitectureFixtureProbeRecordedIntegrationEvent` record in the same file style (`sealed record`, XML doc calling out "infrastructure/demo-only").
9. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/SquadCrm.Modules.ArchitectureFixture.Contracts.csproj` — currently reads *"Public contract surface. Intentionally has NO project references (enforced by SquadCrm.ArchitectureTests)."* This claim becomes **false** the moment this story adds `<ProjectReference Include="....SquadCrm.BuildingBlocks.Abstractions.csproj" />` — update the comment to say the project references **only** `SquadCrm.BuildingBlocks.Abstractions` (Ruling 2).
10. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/ArchitectureFixtureModule.cs` — **whole `RegisterServices`**. **Delete** the `services.AddHttpContextAccessor()` call if this story previously reasoned about adding it — do not add it here at all: a module's persistence must not depend on `HttpContext` (B2). The correlation accessor is registered in the **host**, not the module.
11. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/SquadCrm.BuildingBlocks.csproj` — **whole file**. Gains one `<ProjectReference>` to the new `SquadCrm.BuildingBlocks.Abstractions.csproj`. The `FrameworkReference` stays; no EF Core, no Npgsql, no new package reference.
12. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Correlation/CorrelationIdMiddleware.cs` — **whole file (68 lines)**. `HeaderName`/`MaxLength` (lines 17-20) and the `Generate()` fallback (line 66-67, `Guid.NewGuid().ToString("n")`) are the pattern the new `ICorrelationIdAccessor` reuses — it does not read the raw header itself, and its no-`HttpContext` fallback matches `Generate()`'s shape.
13. `src/backend/src/Api/SquadCrm.Api/Program.cs` — **whole file**. `builder.Services.AddHttpContextAccessor()` is **already registered here** (existing line, do not duplicate). Add `builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();` near it, **before** `RegisterModules(...)` for readability (registration order does not affect DI resolution, since `AddDbContext`'s factory delegate resolves lazily).
14. `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — **lines 1–49** (`ForbiddenAssemblyPrefixes` and its doc comment, which names CRM-198). **This story does NOT remove `Hangfire` or `MediatR` from this list** — neither is introduced. Confirm after implementation with the grep in Task 8 that neither package appears anywhere in the diff.
15. `src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs` — **whole file**. `ModuleContracts_MustNotDependOnEfCoreOrNpgsql` and `BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` must still pass unmodified. Task 7 adds **new** tests alongside these, following the same `AssertReferencesNoEfCoreOrNpgsql`-style helper pattern — read the helper before adding new assertions so the new checks use exact assembly-name equality, not a namespace-style prefix match (see Task 7's caution about `SquadCrm.BuildingBlocks` vs `SquadCrm.BuildingBlocks.Abstractions`).
16. `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` — **whole file (66 lines)**. This story **does** add a new assembly (`SquadCrm.BuildingBlocks.Abstractions`) — add an `Abstractions` property and include it in `All` (Ruling 2 requires this explicitly).
17. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PersistenceRoundTripTests.cs` — **whole file (47 lines)**. Update the `new PersistenceProbe { ... }` object-initializer call to `PersistenceProbe.Record(...)` (Task 3) — the test's own assertions are otherwise unchanged. Its own doc comment ("...CRM-198 owns the durable path") stays accurate.
18. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresTestDatabase.cs` — **whole file (89 lines)**. `CreateContext()` (line ~73) calls `new ArchitectureFixtureDbContextFactory().CreateDbContext([])` — **this is the design-time factory path**, so it exercises the exact same `ArchitectureFixtureDbContextOptions.Apply` call, and therefore the exact same interceptor wiring, as `dotnet ef`. No second test-only wiring path is needed.
19. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/Migrations/20260826165421_InitialArchitectureFixturePersistence.cs` — **whole file**. The new migration (Task 6) follows this exact shape: `EnsureSchema` (already applied, so the new migration only needs `CreateTable`), lowercase snake_case columns, explicit `type:` strings.

Grep hints while implementing:

- `grep -rn "Hangfire\|MediatR" src/backend/src src/backend/tests --include=*.cs --include=*.csproj` — must stay empty after this story; if either package is genuinely needed, escalate rather than add it silently.
- `grep -rn "IOutboxMessageStore\|EfOutboxMessageStore\|ClaimPendingAsync" src/backend` — must be empty after this story; these names must not appear anywhere in the diff (Ruling 1/3).
- `grep -rn "OutboxMessage\|IDomainEvent\|IIntegrationEvent" src/backend/src` — use after Task 2/4/5 to confirm the new types are referenced only where intended.
- `grep -n "CRM-198" src/backend/README.md src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — every placeholder this story is expected to resolve or explicitly leave.

---

## Decisions this plan makes (record these; do not re-litigate during implementation)

| Decision | Choice | Rationale |
|---|---|---|
| Scope boundary (AC 4 / AC 5) | **CLOSED.** AC 4 → CRM-199. AC 5 → CRM-201. CRM-198 delivers exactly: domain events raised in-module, integration-event contracts, transactional outbox write. | User ruling, recorded in Linear. Not an open decision any more. |
| Contracts home | New dependency-free project `SquadCrm.BuildingBlocks.Abstractions` holding only `IDomainEvent`/`IIntegrationEvent`. Module `*.Contracts` projects reference **only** this project. | User ruling 2 — lets `*.Contracts` assemblies declare `IIntegrationEvent` implementations without dragging ASP.NET Core or infrastructure across the contract boundary. |
| Where `OutboxMessage` lives | Plain C# class **inside the ArchitectureFixture module's own `Persistence/` folder** (not `BuildingBlocks`, not `Contracts`); each module that needs one defines and maps its own copy to its own table in its own schema. | User ruling 2 — `OutboxMessage` is a persistence implementation detail, not a shared/reusable abstraction; no second module exists yet to justify a shared type (YAGNI). |
| Domain event raising mechanism | A `HasDomainEvents` mutable-list base (`AddDomainEvent`/`DomainEvents`) in `SquadCrm.BuildingBlocks.Events`, referencing `IDomainEvent` from Abstractions. No dispatcher/mediator. `ClearDomainEvents()` is **not public** — only the interceptor (same assembly boundary via `internal` visibility is not achievable across assemblies, so this is enforced by convention/code review, documented in the XML doc as "drain-only, call after translating"). | AC 1 only requires domain events can be *raised*; no MediatR (forbidden), no current second consumer to justify a bus (YAGNI). |
| Save-path mechanism | `ISaveChangesInterceptor` (`SavingChanges` + `SavingChangesAsync`), registered via `options.AddInterceptors(...)` inside `ArchitectureFixtureDbContextOptions.Apply` — the one place both the runtime `AddDbContext` and the design-time factory call. **Not** a `SaveChangesAsync(CancellationToken)` override. | B1 — an override only intercepts one of the four `SaveChanges*` overloads (`SaveChanges()`, `SaveChanges(bool)`, `SaveChangesAsync(bool, ct)` are missed), so a business row could commit with no outbox row. The interceptor hook fires for every overload and for `SaveChanges` called after an explicit `BeginTransactionAsync` (as `PersistenceRoundTripTests` does) — same transaction either way. |
| Correlation id source | New `ICorrelationIdAccessor` (`SquadCrm.BuildingBlocks.Correlation`), backed by `HttpContext.TraceIdentifier` where available, registered in the **host** (`Program.cs`), injected into the interceptor. No `Func<string>` constructor parameter on the `DbContext`, no `AddHttpContextAccessor()` inside the module. | B2 — the constructor-injected-`Func<string>` scheme cannot work (`AddDbContext` configures options, not constructor arguments); a module's persistence layer must not depend on `HttpContext` directly (wrong dependency direction). |
| `IIntegrationEvent` shape | Exactly `Guid EventId`, `DateTimeOffset OccurredAtUtc`, `string Type` (the stable, explicitly-declared versioned contract name, e.g. `"architecture-fixture.probe-recorded.v1"` — never `nameof`/CLR type name). Nothing else. | B4 (versioned name is durable data, ADR-005 requires versionable contracts) + B5 (`EventId` travels with the message so CRM-199 can dedupe on it later; adding a member to `IIntegrationEvent` after N modules implement it is a breaking change — YAGNI criterion (d) justifies adding it now while there is exactly one implementer). |
| `OutboxMessage.Type` value | Set from the integration event's own `Type` property (the versioned contract-name string), not `nameof(...)` or a CLR type name. | B4. |
| Retry/idempotency/observability surface | **Deleted from this story entirely.** `ProcessedAtUtc`/`RetryCount`/`Error` remain as **columns only** (Fields Dictionary requirement), always written `null`/`null`/`0` by this story, with no mutator API (`MarkProcessed`/`MarkFailed` deleted — no caller exists in CRM-198). `IOutboxMessageStore`/`EfOutboxMessageStore` deleted outright. | Ruling 1 — CRM-199 owns claiming/publishing; CRM-201 owns observability. No abstraction is built for a consumer that does not yet exist (YAGNI). |
| Consumer idempotency (business rule, now CRM-199's) | Not this story's concern; `IIntegrationEvent.EventId` is documented as the future dedupe key. | Ruling 1 — the business rule "Event consumers must be idempotent" moved to CRM-199. |
| Proof vehicle | Extend the existing `ArchitectureFixture` module/fixture (`PersistenceProbe` raises a demonstration domain event; `ArchitectureFixtureProbeRecordedIntegrationEvent` is the demonstration integration event). No new module. | Matches CRM-105/106's own precedent; the fixture is explicitly deletable once real modules exist. |
| `ForbiddenAssemblyPrefixes` (`ArchitectureRulesTests.cs`) | **Unchanged.** `Hangfire` and `MediatR` stay forbidden. | Neither is introduced by this story; the doc comment naming CRM-198 is updated to say so explicitly (Task 9), not to remove an entry. |
| Payload column type | `text`, not `jsonb`. | N5 — `jsonb` reorders object keys and destroys byte-for-byte fidelity of the serialized payload; this is durable data, not queryable JSON in this story. |
| `OutboxMessage.OccurredAtUtc` source | Set by the interceptor from the writer's own clock (`DateTimeOffset.UtcNow`) at save time — **not** copied from the domain/integration event's business timestamp. | N2 — the outbox row's `OccurredAtUtc` is about write/ordering time, not business-event time; the integration event's own `OccurredAtUtc` (business time) is preserved inside `Payload`. |
| Serializer | One shared `JsonSerializerOptions` instance, declared once (`static readonly` field on the interceptor). | N7. |
| Unknown domain event type in the translation switch | `default:` branch throws `InvalidOperationException` naming the unhandled event's CLR type. | N8 — a silently dropped domain event is a real, dangerous gap the moment a second event type is added without a matching translation branch. |

---

## Backend Tasks

### 1 — `SquadCrm.BuildingBlocks.Abstractions` (new project)

**Create project `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks.Abstractions/SquadCrm.BuildingBlocks.Abstractions.csproj`:**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>SquadCrm.BuildingBlocks.Abstractions</RootNamespace>
  </PropertyGroup>

  <!-- Deliberately dependency-free: no FrameworkReference, no PackageReference,
       no ProjectReference. Module *.Contracts projects reference ONLY this
       project so a contract that implements IIntegrationEvent never drags
       ASP.NET Core or infrastructure across the contract boundary
       (SquadCrm.ArchitectureTests enforces this). -->

</Project>
```

Add it to `SquadCrm.sln` under the existing `BuildingBlocks` solution folder (`{90298CBA-BD6F-3A0A-69D5-97CAD7B05E7E}`), matching the existing `SquadCrm.BuildingBlocks` entry's GUID-type prefix (`{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`).

**Create file: `Events/IDomainEvent.cs`**

```csharp
namespace SquadCrm.BuildingBlocks.Abstractions.Events;

/// <summary>
/// Marker for an event raised and consumed <b>inside the owning module only</b>.
/// A domain event never crosses a module boundary; when another module or an
/// external consumer needs to know, the owning module translates it into an
/// explicit <see cref="IIntegrationEvent"/> (ADR-005).
/// </summary>
public interface IDomainEvent
{
    /// <summary>UTC instant the event was raised.</summary>
    DateTimeOffset OccurredAtUtc { get; }
}
```

**Create file: `Events/IIntegrationEvent.cs`**

```csharp
namespace SquadCrm.BuildingBlocks.Abstractions.Events;

/// <summary>
/// Marker for an explicit, versionable cross-module/external contract (ADR-005).
/// Implementations live in each module's own <c>*.Contracts</c> project — never
/// in <c>BuildingBlocks</c>, which stays generic — so a consuming module depends
/// only on the producing module's contracts, never its implementation.
/// Deliberately minimal: exactly the three members below. No routing key, no
/// version-negotiation surface, no headers dictionary — none has a current
/// consumer (YAGNI).
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Stable identity of this specific event occurrence. Copied verbatim into
    /// <c>OutboxMessage.Id</c>. Outbox delivery is at-least-once; this is the
    /// key a future consumer (CRM-199/downstream) dedupes on.
    /// </summary>
    Guid EventId { get; }

    /// <summary>UTC instant the underlying business change occurred.</summary>
    DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Stable, explicitly-declared, versioned contract name (e.g.
    /// <c>"architecture-fixture.probe-recorded.v1"</c>) — never
    /// <c>nameof(...)</c> or a CLR type name, which breaks on rename/refactor.
    /// This value is durable data (persisted verbatim as <c>OutboxMessage.Type</c>)
    /// and append-only: once published, a version segment is never reused for a
    /// different payload shape.
    /// </summary>
    string Type { get; }
}
```

### 2 — `HasDomainEvents` base in `SquadCrm.BuildingBlocks`

**File: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/SquadCrm.BuildingBlocks.csproj`** — add:

```xml
<ItemGroup>
  <ProjectReference Include="../SquadCrm.BuildingBlocks.Abstractions/SquadCrm.BuildingBlocks.Abstractions.csproj" />
</ItemGroup>
```

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Events/HasDomainEvents.cs`**

```csharp
using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.BuildingBlocks.Events;

/// <summary>
/// Opt-in base for an entity that raises domain events inside its own module
/// (AC 1). Deliberately minimal: no dispatcher, no mediator, no bus — a
/// module's own <c>ISaveChangesInterceptor</c> drains <see cref="DomainEvents"/>
/// and decides, per event, whether to translate it into a durable
/// <c>OutboxMessage</c>, then clears them before the underlying <c>SaveChanges</c>
/// call commits.
/// </summary>
public abstract class HasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised and not yet drained.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>
    /// Drain-only: intended to be called by the owning module's save-changes
    /// interceptor immediately after translating every pending event, never by
    /// application/business code.
    /// </summary>
    internal void ClearDomainEvents() => _domainEvents.Clear();
}
```

`ClearDomainEvents()` is `internal` to `SquadCrm.BuildingBlocks` — but the interceptor that needs to call it lives in the **module's** assembly (`SquadCrm.Modules.ArchitectureFixture`), not in `SquadCrm.BuildingBlocks` itself. Two options exist here; **pick one during implementation and do not silently choose**:

- (a) make `ClearDomainEvents()` `public` (simplest; the YAGNI trim only asked to make it not-public where that is achievable without contradicting the interceptor's actual call site — cross-assembly `internal` cannot be satisfied without `InternalsVisibleTo`), or
- (b) add `[assembly: InternalsVisibleTo("SquadCrm.Modules.ArchitectureFixture")]` to `SquadCrm.BuildingBlocks`, which will need updating for every future module that raises domain events.

Given (b) does not scale past one module and this codebase does not use `InternalsVisibleTo` elsewhere (confirm with `grep -rn "InternalsVisibleTo" src/backend/src`), **default to (a): `public void ClearDomainEvents()`**, documented with the same "drain-only, interceptor calls this" XML doc. This is a deliberate, minor deviation from the YAGNI trim's literal wording, made necessary by assembly boundaries — note it in the implementation report; it is not an architectural change requiring escalation.

### 3 — Correlation id accessor abstraction

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Correlation/ICorrelationIdAccessor.cs`**

```csharp
namespace SquadCrm.BuildingBlocks.Correlation;

/// <summary>
/// Reads the current request's correlation id without any consumer depending
/// on <c>HttpContext</c> directly. Registered in the host composition root;
/// a module's persistence layer injects this interface, never
/// <c>IHttpContextAccessor</c> (CLAUDE.md: providers/cross-cutting concerns
/// stay behind provider-neutral ports).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// The current <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/>
    /// when one is available; otherwise a freshly generated id, matching
    /// <see cref="CorrelationIdMiddleware"/>'s own no-request fallback shape.
    /// Never longer than <see cref="CorrelationIdMiddleware.MaxLength"/>.
    /// </summary>
    string Current { get; }
}
```

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Correlation/HttpContextCorrelationIdAccessor.cs`**

```csharp
using Microsoft.AspNetCore.Http;

namespace SquadCrm.BuildingBlocks.Correlation;

internal sealed class HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    : ICorrelationIdAccessor
{
    public string Current =>
        httpContextAccessor.HttpContext?.TraceIdentifier is { Length: > 0 } traceId
            ? traceId
            : Guid.NewGuid().ToString("n");
}
```

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`** — after the existing `builder.Services.AddHttpContextAccessor();` line, add:

```csharp
builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
```

with `using SquadCrm.BuildingBlocks.Correlation;` already present (it is, for `CorrelationIdMiddleware`).

### 4 — `OutboxMessage`, its mapping, and the transactional `ISaveChangesInterceptor`

**Create file: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/OutboxMessage.cs`**

```csharp
namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// A durable record of one <c>IIntegrationEvent</c>, written in the same
/// database transaction as the business change that caused it (ADR-005).
/// <b>Persistence implementation detail — not a shared/reusable type.</b> Each
/// module that needs an outbox defines and maps its own copy to its own table
/// in its own schema (schema-per-module, ADR-002); there is no shared
/// <c>SquadCrm.BuildingBlocks</c> outbox type.
/// <para>
/// <see cref="ProcessedAtUtc"/>, <see cref="RetryCount"/> and <see cref="Error"/>
/// are part of this story's Fields Dictionary and are mapped columns, but this
/// story never writes anything other than <c>null</c>/<c>null</c>/<c>0</c> to
/// them — claiming, retrying and marking failure/success is CRM-199's
/// responsibility, once a claim/read-back abstraction exists to call.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>The integration event's own stable, versioned contract name (e.g. <c>"architecture-fixture.probe-recorded.v1"</c>). Durable data — see <c>IIntegrationEvent.Type</c>.</summary>
    public required string Type { get; init; }

    /// <summary>The event serialized as JSON text (not <c>jsonb</c> — byte-for-byte fidelity, no key reordering).</summary>
    public required string Payload { get; init; }

    /// <summary>Writer's clock at save time — write/ordering time, not the business event's own timestamp (which is inside <see cref="Payload"/>).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Null until a future story's processor marks this message delivered. Always null as written by this story.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    /// <summary>Always 0 as written by this story. A future story's processor increments this on failure.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Always null as written by this story. Truncated/sanitised at the write site by whichever story writes it — never a secret/PII.</summary>
    public string? Error { get; private set; }

    public required string CorrelationId { get; init; }
}
```

**Create file: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/OutboxMessageConfiguration.cs`** — mirror `PersistenceProbeConfiguration.cs` exactly:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(ArchitectureFixtureSchema.OutboxTable, ArchitectureFixtureSchema.Name);

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id");

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(message => message.Payload)
            .HasColumnName("payload")
            .HasColumnType("text")
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(message => message.ProcessedAtUtc)
            .HasColumnName("processed_at_utc")
            .HasColumnType("timestamptz");

        builder.Property(message => message.RetryCount)
            .HasColumnName("retry_count");

        builder.Property(message => message.Error)
            .HasColumnName("error")
            .HasMaxLength(2000);

        builder.Property(message => message.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(message => message.ProcessedAtUtc)
            .HasFilter("processed_at_utc IS NULL")
            .HasDatabaseName("ix_outbox_message_pending");

        // No foreign key: cross-schema/cross-module foreign keys are not the
        // modular-monolith integration mechanism.
    }
}
```

(N3: the partial index anticipates the pending-row lookup CRM-199 will need; no retention/purge story owns this table yet — call that out in the migration and README, Task 9-10.)

**File: `ArchitectureFixtureSchema.cs`** — add after `ProbeTable`:

```csharp
    /// <summary>This module's own outbox table (CRM-198). Not shared with any other module's schema.</summary>
    public const string OutboxTable = "outbox_message";
```

**File: `PersistenceProbeConfiguration.cs`** — add (B6, model build fails without it once `PersistenceProbe` gains `DomainEvents`):

```csharp
        builder.Ignore(probe => probe.DomainEvents);
```

**File: `ArchitectureFixtureDbContext.cs`** — add `DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();` alongside `PersistenceProbes`, and `modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());` alongside the existing `ApplyConfiguration` call. **No** `SaveChangesAsync` override — the interceptor (below) does the work. The primary-constructor shape stays exactly as-is (N14).

**Create file: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureOutboxInterceptor.cs`**

```csharp
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Drains domain events raised on any tracked <see cref="HasDomainEvents"/>
/// entity, translates each into this module's <c>IIntegrationEvent</c>
/// contract(s), and adds the corresponding <see cref="OutboxMessage"/> row to
/// the SAME change tracker before <c>SaveChanges</c> commits — proving
/// atomicity via EF Core's single-transaction guarantee. Fires for every
/// <c>SaveChanges</c>/<c>SaveChangesAsync</c> overload (unlike a
/// <c>SaveChangesAsync(CancellationToken)</c> override, which misses three of
/// the four) and behaves identically whether or not the caller already opened
/// an explicit <c>BeginTransactionAsync</c> — EF Core enlists the interceptor's
/// work in whichever transaction is already open.
/// <para>
/// Module-internal and module-specific by design: the domain-event-to-
/// integration-event translation is this module's own knowledge. There is no
/// generic/shared cross-module event-translation framework (YAGNI) — a second
/// module defines its own interceptor the same way.
/// </para>
/// </summary>
internal sealed class ArchitectureFixtureOutboxInterceptor(ICorrelationIdAccessor? correlationIdAccessor = null)
    : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddOutboxMessagesForPendingDomainEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddOutboxMessagesForPendingDomainEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddOutboxMessagesForPendingDomainEvents(DbContext? context)
    {
        if (context is not ArchitectureFixtureDbContext fixtureContext)
        {
            return;
        }

        string correlationId = correlationIdAccessor?.Current ?? Guid.NewGuid().ToString("n");
        DateTimeOffset writtenAtUtc = DateTimeOffset.UtcNow;

        foreach (EntityEntry<HasDomainEvents> entry in fixtureContext.ChangeTracker.Entries<HasDomainEvents>())
        {
            HasDomainEvents entity = entry.Entity;

            if (entity.DomainEvents.Count == 0)
            {
                continue;
            }

            foreach (IDomainEvent domainEvent in entity.DomainEvents)
            {
                IIntegrationEvent integrationEvent = Translate(domainEvent);

                fixtureContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = integrationEvent.EventId,
                    Type = integrationEvent.Type,
                    Payload = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), SerializerOptions),
                    OccurredAtUtc = writtenAtUtc,
                    CorrelationId = correlationId,
                });
            }

            entity.ClearDomainEvents();
        }
    }

    /// <summary>
    /// Explicit per-event-type translation. A new domain event type without a
    /// matching branch throws rather than being silently dropped (N8) — the
    /// next module author adding a second event type must extend this switch.
    /// </summary>
    private static IIntegrationEvent Translate(IDomainEvent domainEvent) => domainEvent switch
    {
        PersistenceProbeRecordedDomainEvent recorded => new ArchitectureFixtureProbeRecordedIntegrationEvent(
            Guid.NewGuid(), recorded.ProbeId, recorded.Label, recorded.OccurredAtUtc),
        _ => throw new InvalidOperationException(
            $"No integration-event translation registered for domain event type '{domainEvent.GetType()}'."),
    };
}
```

**File: `ArchitectureFixtureDbContextOptions.cs`** — the **single** wiring point for both the runtime and design-time paths (B1):

```csharp
    public static DbContextOptionsBuilder Apply(
        DbContextOptionsBuilder options,
        string connectionString,
        ICorrelationIdAccessor? correlationIdAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(
                    ArchitectureFixtureSchema.MigrationsHistoryTable,
                    ArchitectureFixtureSchema.Name))
            .AddInterceptors(new ArchitectureFixtureOutboxInterceptor(correlationIdAccessor));
    }
```

**File: `ArchitectureFixtureDbContextFactory.cs`** — no functional change to its call site (`ArchitectureFixtureDbContextOptions.Apply(options, connectionString)`); the new optional parameter defaults to `null`, so design time gets the interceptor with the no-`HttpContext` fallback (a freshly generated correlation id per save) automatically. Confirm this compiles unchanged.

**File: `ArchitectureFixtureModule.cs`** — in `RegisterServices`, change the `AddDbContext<ArchitectureFixtureDbContext>` registration to resolve `ICorrelationIdAccessor` from the container (registered by the host, Task 3):

```csharp
        services.AddDbContext<ArchitectureFixtureDbContext>((serviceProvider, options) =>
            ArchitectureFixtureDbContextOptions.Apply(
                options,
                configuration.GetSquadCrmPostgresConnectionString(),
                serviceProvider.GetRequiredService<ICorrelationIdAccessor>()));
```

Do **not** add `services.AddHttpContextAccessor()` here — it is already registered by the host (`Program.cs`), and a module registering it again would be the wrong dependency direction even if harmless in practice (B2).

### 5 — Wire `PersistenceProbe` to raise the demonstration domain event

**File: `PersistenceProbe.cs`**

```csharp
using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Events;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

public sealed class PersistenceProbe : HasDomainEvents
{
    public required Guid Id { get; init; }
    public required string Label { get; init; }
    public DateTimeOffset RecordedAtUtc { get; init; }

    public static PersistenceProbe Record(Guid id, string label, DateTimeOffset recordedAtUtc)
    {
        PersistenceProbe probe = new() { Id = id, Label = label, RecordedAtUtc = recordedAtUtc };
        probe.AddDomainEvent(new PersistenceProbeRecordedDomainEvent(id, label, recordedAtUtc));
        return probe;
    }
}

internal sealed record PersistenceProbeRecordedDomainEvent(Guid ProbeId, string Label, DateTimeOffset OccurredAtUtc)
    : IDomainEvent;
```

Update `PersistenceRoundTripTests.cs` (`context.PersistenceProbes.Add(new PersistenceProbe { ... })`) to call `PersistenceProbe.Record(id, nameof(...), recordedAt)` instead — confirm this is the only production/test call site (`grep -rn "new PersistenceProbe" src/backend`).

**Create file: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/ArchitectureFixtureProbeRecordedIntegrationEvent.cs`**

```csharp
using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.ArchitectureFixture.Contracts;

/// <summary>
/// Infrastructure/demo-only integration-event contract. Proves that a domain
/// event raised inside the module is translated into an explicit, versionable
/// cross-module contract before it leaves the module (ADR-005). Not a CRM
/// capability; deleted with the rest of the fixture.
/// </summary>
public sealed record ArchitectureFixtureProbeRecordedIntegrationEvent(
    Guid EventId,
    Guid ProbeId,
    string Label,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "architecture-fixture.probe-recorded.v1";

    public string Type => ContractName;
}
```

**File: `SquadCrm.Modules.ArchitectureFixture.Contracts.csproj`** — add and update the comment:

```xml
  <!-- Public contract surface. References ONLY SquadCrm.BuildingBlocks.Abstractions
       (enforced by SquadCrm.ArchitectureTests) — never SquadCrm.BuildingBlocks
       itself, which carries ASP.NET Core and would drag it across the contract
       boundary into every consumer of this project. -->
  <ItemGroup>
    <ProjectReference Include="../../../BuildingBlocks/SquadCrm.BuildingBlocks.Abstractions/SquadCrm.BuildingBlocks.Abstractions.csproj" />
  </ItemGroup>
```

### 6 — Migration

Run from `src/backend/`, with the `POSTGRES_*` env values loaded:

```bash
set -a && . ../../env/backend.env && set +a
dotnet ef migrations add AddOutboxMessage \
  --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --context ArchitectureFixtureDbContext \
  --output-dir Persistence/Migrations
```

Confirm the generated migration only adds `CreateTable("outbox_message", schema: "architecture_fixture", ...)` and the partial index — no `EnsureSchema` (already applied), no change to `persistence_probe`. Add a migration-file comment (or a `README.md` note, Task 9) stating explicitly: no story yet owns retention/purge of processed outbox rows (N3).

### 7 — Architecture tests: protect the new dependency boundary

**File: `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs`** — add:

```csharp
    public static Assembly Abstractions { get; } = typeof(IDomainEvent).Assembly;
```

(with `using SquadCrm.BuildingBlocks.Abstractions.Events;`), and add `Abstractions` to the `All` array. Add `<ProjectReference>` to `SquadCrm.BuildingBlocks.Abstractions.csproj` in `SquadCrm.ArchitectureTests.csproj`.

**File: `src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs`** (or a new file `ContractsArchitectureRulesTests.cs` if that reads more cleanly alongside the existing split — controller's call, not a decision this plan needs to make) — add two tests:

```csharp
    /// <summary>
    /// Ruling 2: Abstractions is deliberately dependency-free so *.Contracts
    /// assemblies can reference it without inheriting ASP.NET Core or
    /// infrastructure. A stray package/project/framework reference here would
    /// silently reintroduce that coupling for every future module.
    /// </summary>
    [Fact]
    public void Abstractions_MustHaveNoDependencies()
    {
        IReadOnlyList<string> referenced = SquadCrmAssemblies.ReferencedAssemblyNames(SquadCrmAssemblies.Abstractions);

        // Exact-name checks only — a substring/prefix check would false-positive
        // on "SquadCrm.BuildingBlocks.Abstractions" itself containing
        // "SquadCrm.BuildingBlocks" as a string prefix.
        Assert.DoesNotContain(referenced, name => name.StartsWith("SquadCrm.", StringComparison.Ordinal));
        Assert.DoesNotContain(referenced, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ruling 2: a module's *.Contracts project may implement IIntegrationEvent,
    /// but must never reference the ASP.NET-Core-bearing SquadCrm.BuildingBlocks
    /// — only SquadCrm.BuildingBlocks.Abstractions.
    /// </summary>
    [Fact]
    public void ContractsAssemblies_MustNotDependOnBuildingBlocks()
    {
        Assembly[] contracts = SquadCrmAssemblies.All
            .Where(assembly => assembly.GetName().Name!
                .EndsWith(SquadCrmAssemblies.ContractsSuffix, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(contracts);

        foreach (Assembly assembly in contracts)
        {
            IReadOnlyList<string> referenced = SquadCrmAssemblies.ReferencedAssemblyNames(assembly);

            // Exact equality, not StartsWith: "SquadCrm.BuildingBlocks.Abstractions"
            // must remain allowed while "SquadCrm.BuildingBlocks" itself is forbidden.
            Assert.DoesNotContain(SquadCrmAssemblies.BuildingBlocksName, referenced);
        }
    }
```

Read `AssertReferencesNoEfCoreOrNpgsql`'s existing implementation first and match its exact-match style rather than inventing a second convention.

### 8 — Confirm no forbidden dependency or deleted abstraction was reintroduced

```bash
grep -rn "Hangfire\|MediatR" src/backend/src src/backend/tests --include=*.cs --include=*.csproj
grep -rn "IOutboxMessageStore\|EfOutboxMessageStore\|ClaimPendingAsync\|MarkProcessedAsync\|MarkFailedAsync" src/backend
```

Both must return no matches. If a genuine need surfaces during implementation, **stop and escalate** — do not add either silently.

### 9 — Integration test: outbox row commits atomically with the business row

**Create file: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/OutboxTransactionalWriteTests.cs`**, `[Collection(PostgresTestDatabase.CollectionName)]` like `PersistenceRoundTripTests.cs`:

1. `Probe_And_OutboxMessage_CommitTogether` — `using ArchitectureFixtureDbContext context = PostgresTestDatabase.CreateContext();`, `await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();` (proves the interceptor works when the caller has already opened an explicit transaction, per B1), add a probe via `PersistenceProbe.Record(...)`, call `SaveChangesAsync()`, then assert both `context.PersistenceProbes.SingleAsync(p => p.Id == id)` and `context.OutboxMessages.SingleAsync(m => m.CorrelationId == <known-generated-id> && m.Id == <probe-derived-EventId>)` are present, with the outbox row's `Type == ArchitectureFixtureProbeRecordedIntegrationEvent.ContractName` and `ProcessedAtUtc == null`. Scope every assertion by the correlation id/event id actually produced (N11) rather than by table-wide counts, so the test is safe to run against a shared dev database. Roll back via `transaction.RollbackAsync()` so no row survives the test.
2. `SaveChanges_NonAsyncOverload_AlsoWritesOutboxRow` — same shape as (1) but calling the synchronous `context.SaveChanges()` — proves the interceptor's `SavingChanges` (not just `SavingChangesAsync`) hook fires, which a `SaveChangesAsync(CancellationToken)` override would have missed (B1's exact failure mode). Roll back the same way.
3. `UnhandledDomainEvent_Throws` (optional but recommended — cheap to add, directly proves N8's `default:` guard) — construct a `PersistenceProbe`-like scenario is not straightforward without a second domain event type; if it cannot be exercised without inventing a second event type purely for the test, **skip this case** and instead cover it with a plain unit-style assertion against the `Translate` method if it is made `internal` and testable, or note in the test file why it is not exercised (a real second domain event type does not exist yet — YAGNI, do not invent one solely to test the `default:` branch).

Wrap each test body in a transaction that is always rolled back (N11), and scope every assertion by the row(s) that test itself created.

### 10 — Update `ArchitectureRulesTests.cs` doc comment

**File: `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`** — the doc comment currently says CRM-198 "must update this list when [it] legitimately introduce[s] one of these [Hangfire/MediatR]." Update the CRM-198 clause to state CRM-198 evaluated both and introduced neither — domain events are drained synchronously inside an EF `SaveChangesInterceptor`, and any future claim/publish mechanism is CRM-199's to build — so `ForbiddenAssemblyPrefixes` is unchanged by this story. **Do not remove any entry from the array.**

### 11 — Update `src/backend/README.md`

Add a short "Domain events, integration events & the outbox" subsection (near the existing **Persistence** section) describing: `SquadCrm.BuildingBlocks.Abstractions` (`IDomainEvent`, `IIntegrationEvent`), `SquadCrm.BuildingBlocks.Events` (`HasDomainEvents`), `SquadCrm.BuildingBlocks.Correlation.ICorrelationIdAccessor`, the per-module `OutboxMessage`/outbox-table pattern (module-owned, not shared), the "one `SaveChanges` call = one transaction, driven by an `ISaveChangesInterceptor`" guarantee, and explicitly:

> Claiming/publishing pending outbox rows on a schedule, retrying failed deliveries, and consumer idempotency are CRM-199's scope. Structured observability of `RetryCount`/`Error`/processing status is CRM-201's scope. This story adds neither — `ProcessedAtUtc`, `RetryCount` and `Error` exist as columns only, always written `null`/`0`/`null` here.
>
> Integration-event payloads carry identifiers, not secrets/PII. `Error` (once a future story writes to it) must be truncated and sanitised at the write site — never a raw exception message or stack trace. `Payload` is stored as `text`, not `jsonb`, to preserve exact byte fidelity; no story yet owns retention/purge of processed rows (the `ix_outbox_message_pending` partial index only accelerates the pending-row lookup).

---

## Edge Cases & Failure Modes

- **`SaveChanges`/`SaveChangesAsync` throws after the interceptor adds an `OutboxMessage`, before the database commits** (e.g. a unique-constraint violation on the business row) — the whole transaction rolls back, so no orphan outbox row is written. Ordinary EF Core transaction semantics; proven by Task 9.1/9.2 succeeding, not by a dedicated failure test (the guarantee is EF Core's, not this story's, to re-prove).
- **A tracked entity raises a domain event with no translation case in `Translate`** — throws `InvalidOperationException` naming the CLR type, rather than silently dropping it (N8). This is a deliberate fail-fast: the next module author adding a second domain event type must extend the switch.
- **`SaveChanges`/`SaveChangesAsync` called with no pending domain events** — the interceptor's loop finds zero `HasDomainEvents` entries with a non-empty `DomainEvents` collection and is a no-op; ordinary saves (e.g. an unrelated future entity) are unaffected.
- **`CorrelationId` outside an HTTP request** (design-time factory, or a future background job) — `correlationIdAccessor` is `null` in that path, and the interceptor falls back to a freshly generated id per save, matching `CorrelationIdMiddleware.Generate()`'s own fallback shape.
- **Migration applied to a database that already has `architecture_fixture.persistence_probe`** — the new migration only adds `outbox_message` and its partial index; `persistence_probe` and its existing migration are untouched.
- **A future consumer needs to claim/process pending rows** — explicitly out of scope here. `ProcessedAtUtc IS NULL` is the pending predicate the partial index accelerates; no claim/lease/concurrency-safety mechanism exists yet (CRM-199's problem, once it exists).

---

## Test Plan

1. **`src/backend/tests/SquadCrm.Persistence.IntegrationTests/OutboxTransactionalWriteTests.cs`** (new, real PostgreSQL required) — Task 9's tests.
2. **`src/backend/tests/SquadCrm.Persistence.IntegrationTests/PersistenceRoundTripTests.cs`** (modified) — `new PersistenceProbe { ... }` → `PersistenceProbe.Record(...)`; existing assertions otherwise unchanged.
3. **`src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs`** (modified) — `Abstractions_MustHaveNoDependencies` and `ContractsAssemblies_MustNotDependOnBuildingBlocks` (Task 7, new); `BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` and `ModuleContracts_MustNotDependOnEfCoreOrNpgsql` re-run as regression, unchanged.
4. **`src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`** (unchanged, re-run as regression) — `Foundation_MustNotIntroduceForbiddenDependencies` must still pass (confirms Task 8's grep programmatically).
5. Unit-level coverage for `HasDomainEvents.AddDomainEvent`/`ClearDomainEvents` and the interceptor's `Translate` switch: cover through the integration tests in Task 9 rather than standing up a new unit-test project — no `SquadCrm.BuildingBlocks.Tests` project exists yet (`find src/backend/tests -iname "*BuildingBlocks*"` — confirm still empty); a dedicated unit-test project for a handful of methods is a call for the controller if judged necessary later, not a default here (YAGNI).

---

## Migration / Rollback

- **Forward:** `dotnet ef database update --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --context ArchitectureFixtureDbContext` from `src/backend/`, env loaded, PostgreSQL running.
- **Rollback:** `dotnet ef database update <previous-migration-name> ...` (the `20260826165421_InitialArchitectureFixturePersistence` migration) to drop `outbox_message` (and its index) via the generated `Down()`.
- **Half-applied state:** PostgreSQL's DDL transaction wraps a single migration, so the table either exists fully (with its index) or not at all. Re-running `database update` is safe and idempotent.

---

## Verification Steps

1. **Backend builds:** `cd src/backend && dotnet build` — zero warnings (`TreatWarningsAsErrors=true`).
2. **Architecture tests:** `cd src/backend && dotnet test tests/SquadCrm.ArchitectureTests` — no database needed; must include `Foundation_MustNotIntroduceForbiddenDependencies`, `Abstractions_MustHaveNoDependencies`, `ContractsAssemblies_MustNotDependOnBuildingBlocks`.
3. **API tests:** `cd src/backend && dotnet test tests/SquadCrm.Api.Tests` — no database needed; confirms host composition (`AddHttpContextAccessor`, `ICorrelationIdAccessor` registration, `RegisterModules`) still succeeds.
4. **Persistence integration tests (real PostgreSQL required):**
   ```bash
   docker compose up -d
   cd src/backend && set -a && . ../../env/backend.env && set +a
   dotnet ef database update --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --context ArchitectureFixtureDbContext
   dotnet test tests/SquadCrm.Persistence.IntegrationTests
   ```
   Must include `OutboxTransactionalWriteTests` and the updated `PersistenceRoundTripTests`.
5. **Clean-recreate regression:** `docker compose down -v && docker compose up -d`, re-run step 4's `dotnet ef database update` and confirm both `persistence_probe` and `outbox_message` (with its partial index) exist afterward.
6. **Forbidden-dependency / deleted-abstraction grep (Task 8):** both greps return no matches.

---

## Done Criteria

- [ ] Domain events can be raised inside a module — `HasDomainEvents`/`IDomainEvent` (Abstractions) exist; `PersistenceProbe.Record(...)` raises one, proven by `OutboxTransactionalWriteTests`.
- [ ] Cross-module/external integration events use explicit contracts — `IIntegrationEvent` (Abstractions); `ArchitectureFixtureProbeRecordedIntegrationEvent` in `SquadCrm.Modules.ArchitectureFixture.Contracts` is the demonstration contract, referencing only `SquadCrm.BuildingBlocks.Abstractions`.
- [ ] Integration events are persisted transactionally with business changes through an outbox — `ArchitectureFixtureOutboxInterceptor` adds the `OutboxMessage` row in the same `SaveChanges`/`SaveChangesAsync` call as the business row, for every overload; proven by `Probe_And_OutboxMessage_CommitTogether` and `SaveChanges_NonAsyncOverload_AlsoWritesOutboxRow`.
- [ ] The outbox table is owned by the module that writes it, in that module's own schema — `outbox_message` lives in `architecture_fixture`, mapped by `OutboxMessageConfiguration`, no shared/cross-module table.
- [ ] Domain events are internal to their owning module unless translated to an integration event — enforced by construction: `PersistenceProbeRecordedDomainEvent` is `internal`; only the translated `IIntegrationEvent` contract is public, in `.Contracts`.
- [ ] Cross-module side effects must not require direct table access — no cross-module table/DbContext reference added; `PersistenceArchitectureRulesTests` passes.
- [ ] A consuming module never reads the producing module's outbox table — no second module exists yet; nothing added that would allow it (no shared type, no cross-module DbSet exposure).
- [ ] Business transaction and outbox persistence succeed/fail together — single `SaveChanges`/`SaveChangesAsync` call via the interceptor; proven by Task 9.1/9.2.
- [ ] `dotnet build`, `dotnet test tests/SquadCrm.ArchitectureTests`, `dotnet test tests/SquadCrm.Api.Tests`, and `dotnet test tests/SquadCrm.Persistence.IntegrationTests` (real PostgreSQL) all pass.
- [ ] No `Hangfire`, `MediatR`, `IOutboxMessageStore`, `EfOutboxMessageStore`, `ClaimPendingAsync`, `MarkProcessedAsync`, or `MarkFailedAsync` anywhere in the diff.
- [ ] `src/backend/README.md` documents the new pattern and explicitly states the CRM-199/CRM-201 boundary and that this story writes `ProcessedAtUtc`/`RetryCount`/`Error` as `null`/`0`/`null` only.

**STOP HERE. Report to the user and wait for confirmation before proceeding to CRM-199.**
