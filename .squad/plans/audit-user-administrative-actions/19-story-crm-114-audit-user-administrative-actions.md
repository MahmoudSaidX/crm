# Story 19 — Audit User and Administrative Actions (Story: CRM-114)

---

## Prerequisites

- Story 17 (`../configure-role-permissions/17-story-crm-113.md`, CRM-113) completed: role/permission catalog, `PermissionPolicies`, `PermissionRequirement`/`PermissionAuthorizationHandler`, `ICurrentUserAccessor`.
- Story 18 (`../manage-staff-users/18-story-crm-111-manage-staff-users.md`, CRM-111) completed: `StaffUserService` create/update/activate/deactivate, staff user list/form screens.
- Story 03 (`../crm-105-aspnet-core-modular-monolith/00-overview.md`, CRM-105) and Story 05 (`../crm-106-postgresql-ef-core-schema-per-module/00-overview.md`, CRM-106) completed: module skeleton (`IModule`, per-module schema/`DbContext`/EF migrations) conventions this story reuses verbatim for the new module.
- No coordination needed with other in-flight stories: this story only adds new files plus a small, additive call site in `AuthorizationBootstrapService` (and its constructor in the `SquadCrm.RoleManagement.Bootstrap` CLI tool's DI setup); it does not modify `StaffUserService`'s or `PermissionService`'s public signatures at all.

---

## Story Goal

Add one reusable, append-only audit-record capability — a new `Audit` module exposing `IAuditRecorder` — and a minimal authorized list/detail UI for it. Prove it end-to-end by wiring it into a real, already-existing administrative operation that today has **no audit trail at all**:

1. `AuthorizationBootstrapService.BootstrapAsync`'s role-assignment write (`dbContext.StaffSubjectRoles.Add(...)`, `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/AuthorizationBootstrapService.cs:57-61`) — the one-time/ops path (invoked by the `SquadCrm.RoleManagement.Bootstrap` CLI tool, `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs`) that grants a staff subject a role. This path already writes a `PermissionChangeAuditEvent` when it also grants missing bootstrap permissions to the role (lines 74-84), but the subject-to-role assignment itself (lines 54-62) has no corresponding audit row in any existing table — unlike `StaffRoleAssignmentService.ReplaceAsync` (`StaffRoleAssignmentService.cs:35-79`), which is the ordinary admin-UI path for role assignment and already writes `StaffRoleAssignmentAuditEvent` for every replacement.

This is a single wiring call site — deliberately not the `StaffUserService`/`PermissionService` operations named in the original intake pass, which the user decided must stay on their existing transactional audit classes (`AuthenticationEvent`, `PermissionChangeAuditEvent`) rather than dual-write to the new mechanism. See `## Known Limitations & Out of Scope` below for the full rationale.

**Not in scope:** analytics/dashboards, export, retention/archival, tamper-evidence (hash chaining, WORM), before/after diffing, a configurable rule engine, event-sourcing, a new cross-module messaging bus, retrofitting every prior story or every other write path (only the one operation above is wired in this story), any change to the existing `RoleManagementDbContext` audit tables (`RoleAuditEvent`, `StaffRoleAssignmentAuditEvent`, `PermissionChangeAuditEvent`) or `StaffIdentityDbContext`'s `AuthenticationEvent`, and consolidating/unifying the two audit mechanisms — see the structural decision and Known Limitations sections below.

### Structural decision — relationship to existing module-local audit classes (flagged for arch-reviewer)

`RoleManagementDbContext` already has three module-local, append-only audit tables (`RoleAuditEvent`, `PermissionChangeAuditEvent`, `StaffRoleAssignmentAuditEvent` — `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/`), and `StaffIdentityDbContext` has a fourth, `AuthenticationEvent` (written by `StaffUserService.RecordEventAsync`, `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/StaffUserService.cs:122-133`, and by `AuthenticationService.RecordEventAsync`). Each of these four is inserted in the exact same `SaveChangesAsync` call as the mutation it records (see `RoleService.cs:60,64` / `104-105`, `PermissionService.cs` `ReplaceAsync`, `StaffRoleAssignmentService.cs:69-77`, `StaffUserService.cs:50-51,69,118`), so each one is **fully transactional** with its business write today — a failure rolls back both together. This is an established, repeated, deliberate pattern in this codebase — see the doc comment on `RoleAuditEvent` (`Persistence/RoleAuditEvent.cs:3-15`), which explicitly says it "mirrors `AuthenticationEvent`" — but it is module-local and never exposed through any list/detail UI, and every write path that has one of these four already covers 100% of the `StaffIdentity`/`RoleManagement` HTTP endpoints (`CreateAsync`/`UpdateAsync`/`Activate`/`Deactivate` on both `Role` and `StaffUser`, `ReplaceRolePermissionsAsync`, `ReplaceStaffRolesAsync`, `/auth/login`/`/refresh`/`/logout`). The only unaudited write found across both modules is the CLI-only bootstrap path named above.

**Recommendation implemented by this plan:** leave all four existing classes and their tables completely untouched — no code changes to any of them. They remain module-internal historical trails for their own module's internal diagnostics (e.g. `RoleAuditEvent.EventType` values like `"created"`/`"updated"` support only `RoleManagementTests.cs`-style assertions today). Add a **new, separate, small `Audit` module** (`src/backend/src/Modules/Audit/`) with its own schema (`audit`), owning a brand-new `AuditRecord` table and its own `AuditDbContext`. It exposes exactly one contract, `IAuditRecorder` (in a new `SquadCrm.Modules.Audit.Contracts` project — no ASP.NET Core dependency), that any module can call to append a record. This is the "one reusable audit-record capability" the intake asks for; it does not duplicate or replace the existing four classes because none of those is reusable across modules today (they are `private`/module-local persistence details), none is surfaced through any UI, and — per the user's explicit decision — this story does not dual-write the same operation to both mechanisms. CRM-114's list/detail screens read only from the new `AuditRecord` table, which after this story holds exactly one kind of event (`role_assigned`, from the bootstrap path) until a future story wires in more.

### Known Limitations & Out of Scope (user decision)

The user reviewed the option of wiring `IAuditRecorder` into `StaffUserService` (user_created/updated/activated/deactivated) and `PermissionService.ReplaceAsync` — both already have a working, transactional, module-local audit trail (`AuthenticationEvent`, `PermissionChangeAuditEvent`) — and explicitly rejected dual-writing the same operation to both the existing class and the new `AuditRecord` table. The following is decided, not an open question:

- **Audit storage is not yet consolidated.** This story adds a second, parallel audit mechanism (`AuditRecord` in the new `audit` schema) alongside the four pre-existing ones; it does not merge them.
- **Pre-CRM-114 operations remain on their existing module-local transactional audit tables and are NOT touched by this story:** `RoleAuditEvent`, `PermissionChangeAuditEvent`, `StaffRoleAssignmentAuditEvent` (all in `RoleManagementDbContext`), and `AuthenticationEvent` (in `StaffIdentityDbContext`). No code, schema, or test file for any of these four is modified.
- **No cross-DbContext transaction is introduced.** The new `AuditRecord` write for this story's one call site is a separate, best-effort, post-commit write — see the transaction-boundary note above.
- **Consolidating/unifying the two audit mechanisms is deferred, non-blocking future hardening.** A future story could migrate the four existing classes' historical data into `AuditRecord` and retire them, or leave them as module-internal diagnostics permanently — that decision is explicitly not made here.
- **No consolidation story is being created now.** This plan does not add a backlog item, ADR, or Linear issue for the consolidation; it is left as a known limitation for a future architecture pass to pick up if and when it becomes valuable.

**Cross-module call mechanism:** a direct DI-resolved interface call (`IAuditRecorder.RecordAsync(...)`), not the domain-events/outbox mechanism from `../domain-events-integration-events-transactional-outbox/07-story-crm-198-domain-events-integration-events-transactional-outbox.md` (CRM-198). That mechanism exists for cross-module **fan-out** where other modules need to react asynchronously to an event; nothing subscribes to an audit write, so forcing it through the outbox would add a background-processing/idempotency dependency (CRM-199, not yet needed here) for no benefit. This mirrors the existing precedent above (`ICurrentUserAccessor`/`IStaffSubjectReferenceReader` are already consumed cross-module as plain DI interfaces — see `RoleManagementModule.cs:44-47` and `AuthorizationBootstrapService.cs:23`), which is the same shape as the new `IAuditRecorder` consumption.

**Transaction boundary (edge case, not an architectural gate):** `IAuditRecorder.RecordAsync` uses `AuditDbContext`, a separate `DbContext`/connection from the caller's own `DbContext` (`RoleManagementDbContext`, for this story's one call site). This is **not** because EF Core cannot share a transaction across schemas — all modules share one physical PostgreSQL connection string, and `Database.UseTransaction(...)` could in principle let two `DbContext`s participate in the same transaction. The real reason is coupling, not capability: sharing a transaction would require threading a transaction/connection handle through the `IAuditRecorder` cross-module contract signature, which would leak the caller's persistence lifetime into a contract that is meant to be a plain, storage-agnostic DI interface (per CLAUDE.md: provider-neutral ports, no cross-module `DbContext` access), and would couple the Audit module's own storage choices to whatever DbContext/transaction shape each caller happens to use. That coupling is the thing this plan avoids, not a technical impossibility. The audit write is therefore called **after** the caller's own `SaveChangesAsync` succeeds, and a failure inside `IAuditRecorder.RecordAsync` is caught and logged, not rethrown — it must never fail or roll back the business operation it is recording. Unlike the four existing module-local audit classes (which are fully transactional with their business write, per the structural decision above), this makes the new `AuditRecord` write for this story's call site genuinely best-effort — the one and only best-effort trail in the codebase after this story ships. This is documented in `## Edge Cases & Failure Modes` below.

---

## Context — Read These Files First

1. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/RoleManagementModule.cs` — read in full (242 lines). This is the module skeleton to copy: `IModule.RegisterServices`/`MapEndpoints`, `AddDbContext` with `MigrationsHistoryTable`, `AddAuthorization` policies, minimal-API endpoint mapping with `RequireAuthorization(PermissionPolicies...)`, `NotFoundProblem()`/`DuplicateProblem()` helpers.
2. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/RoleManagementDbContext.cs` (128 lines) and `Persistence/RoleManagementSchema.cs` — copy the `HasDefaultSchema`/`ToTable`/`HasColumnName` style and the `PermissionDefinition.HasData` seeding pattern (lines 47-84) for the new module's own permission rows.
3. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/RoleAuditEvent.cs` — read in full (24 lines). Do **not** modify. Its doc comment is the precedent cited above.
4. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Contracts/StaffSubjectReference.cs` (14 lines) — this is the exact shape to copy for `IAuditRecorder`: a small interface in a dependency-free `.Contracts` project, implemented in the owning module, consumed cross-module via constructor injection.
5. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/StaffIdentityModule.cs:51` — `services.AddScoped<IStaffSubjectReferenceReader, StaffSubjectReferenceReader>();` and `RoleManagementModule.cs:44-47` (comment) — the exact registration/consumption precedent `IAuditRecorder` follows.
6. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/SquadCrm.Modules.RoleManagement.csproj` and `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Contracts/SquadCrm.Modules.StaffIdentity.Contracts.csproj` — copy the `<ProjectReference>` shape (`SquadCrm.BuildingBlocks`, `SquadCrm.BuildingBlocks.Abstractions`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.EntityFrameworkCore.Design`).
7. `src/backend/tests/SquadCrm.ArchitectureTests/ModuleProjectDependencyRulesTests.cs:1-51` (`Modules_MayReferenceOtherModulesOnlyThroughContractsProjects`) — the enforced rule: any module `.csproj` that is not itself a `*.Contracts` project may only reference another module's implementation project through that module's `*.Contracts` project. `SquadCrm.Modules.StaffIdentity.csproj` and `SquadCrm.Modules.RoleManagement.csproj` must reference `SquadCrm.Modules.Audit.Contracts.csproj`, never `SquadCrm.Modules.Audit.csproj`.
8. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/AuthorizationBootstrapService.cs` — read in full (89 lines). This is the one call site: wire `IAuditRecorder` after the `dbContext.StaffSubjectRoles.Add(...)` block (lines 54-62), following the same `await dbContext.SaveChangesAsync(cancellationToken)` at line 86 that already commits the role assignment and any bootstrap permission grants. Do **not** touch `RoleService.cs` or `StaffRoleAssignmentService.cs` — both are already fully covered by `RoleAuditEvent`/`StaffRoleAssignmentAuditEvent` on every HTTP-reachable path.
9. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/StaffUserService.cs` and `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/PermissionService.cs` — read only to confirm they are **not** touched by this story (per the user decision in `## Known Limitations & Out of Scope`): both already write to `AuthenticationEvent`/`PermissionChangeAuditEvent` in the same `SaveChangesAsync` as their mutation, and dual-writing the same operation to the new `AuditRecord` table was explicitly rejected.
10. `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs` — read in full (69 lines). This CLI tool builds its own `ServiceCollection` (it does not go through `Program.cs`'s module list) and is the only caller of `AuthorizationBootstrapService.BootstrapAsync`. It must register `AuditModule`'s `IAuditRecorder` (or at minimum construct `AuditDbContext`/`AuditRecorder` directly, matching how it already constructs `StaffIdentityDbContext`/`RoleManagementDbContext` directly at lines 37-41) or `AuthorizationBootstrapService`'s new constructor dependency will fail to resolve at runtime.
11. `src/backend/src/Api/SquadCrm.Api/Program.cs:153-154` — the module registration list (`new StaffIdentityModule()`, `new RoleManagementModule()`). Add `new SquadCrm.Modules.Audit.AuditModule()` to this list before `StaffIdentityModule` (so `IAuditRecorder` is registered before any module that consumes it, matching the ordering comment already in `RoleManagementModule.cs:44-47`). Note the API host itself only serves as the module composition root — `AuthorizationBootstrapService` is registered there too (`RoleManagementModule.cs:29`) but is only ever invoked from the CLI tool in item 10, never from an HTTP endpoint.
12. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/RoleManagementTests.cs:1-32` — the test shape to copy for the new module: `[Collection(PostgresTestDatabase.CollectionName)]`, a `CreateXContext()` static factory (grep `PostgresTestDatabase.CreateRoleManagementContext` in `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresTestDatabase.cs` for the exact factory-method pattern to add a `CreateAuditContext()` twin), and assert-by-querying-the-`DbSet` style.
13. `src/backend/tests/SquadCrm.Api.Tests/RoleEndpointsAuthorizationTests.cs` and `StaffUserEndpointsAuthorizationTests.cs` — the pattern for asserting an endpoint returns 401/403 without the right permission and 200/2xx with it; copy for the new `/api/v1/audit-records` endpoints.
14. `src/frontend/projects/agent-crm/src/app/staff-users/staff-user-list.ts` (72 lines), `staff-user-list.html`, `staff-users.service.ts` (88 lines) — the exact component shape to copy for the audit list screen: `TableLazyLoadEvent`/`onLazyLoad`, `signal`-based state, `PagedResult<T>` service call, PrimeNG `TableModule`/`TagModule`.
15. `src/frontend/projects/agent-crm/src/app/staff-users/staff-user-translations.ts` — the `TranslationResources` shape (`en`/`ar` string maps) to copy for `AUDIT_TRANSLATIONS`.
16. `src/frontend/projects/agent-crm/src/app/app.routes.ts:21-59` and `app.config.ts:21-22,36-37` — where to add the new `audit` route (gated by `requirePermission('audit.view')`) and register `AUDIT_TRANSLATIONS` via `provideTranslations(...)`.
17. `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` (86 lines, read in full) and `SquadCrm.ArchitectureTests.csproj` — the hardcoded assembly/project-reference lists that MUST be edited to register the new module (see task 3 below); grep `IStaffSubjectReferenceReader` across `src/backend/src` for the existing cross-module DI precedent (`AuthorizationBootstrapService.cs:23`, `StaffRoleAssignmentService.cs:23`, `StaffIdentityModule.cs:51`, `StaffSubjectReferenceReader.cs:8`, `BootstrapProgram.cs:42`) to confirm the shape `IAuditRecorder` follows.

---

## Backend Tasks

### 1 — `SquadCrm.Modules.Audit.Contracts` project

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit.Contracts/SquadCrm.Modules.Audit.Contracts.csproj`, copying `SquadCrm.Modules.StaffIdentity.Contracts.csproj`'s shape (only a `ProjectReference` to `SquadCrm.BuildingBlocks.Abstractions`).

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit.Contracts/AuditRecordRequest.cs`:

```csharp
namespace SquadCrm.Modules.Audit.Contracts;

public sealed record AuditRecordRequest(
    string ActorHandle,
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IAuditRecorder
{
    Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken);
}
```

`ActorHandle` mirrors the existing `ChangedByHandle`/`currentUserAccessor.Handle` convention (string handle, not a foreign key — same as `RoleAuditEvent.ChangedByHandle`, `PermissionChangeAuditEvent.ChangedByHandle`). `Metadata` is a flat string-to-string map only — no nested objects, no arbitrary payload types — this is a deliberate constraint so callers cannot accidentally serialize a secret-bearing object; callers must build the map explicitly, key by key.

### 2 — `SquadCrm.Modules.Audit` project (implementation)

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/SquadCrm.Modules.Audit.csproj`, copying `SquadCrm.Modules.RoleManagement.csproj`'s shape: `FrameworkReference` to `Microsoft.AspNetCore.App`, `PackageReference` to `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design` (same versions), `ProjectReference` to `SquadCrm.BuildingBlocks`, `SquadCrm.Infrastructure.Postgres`, and this module's own `SquadCrm.Modules.Audit.Contracts`.

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/Persistence/AuditSchema.cs`:

```csharp
namespace SquadCrm.Modules.Audit.Persistence;

internal static class AuditSchema
{
    public const string Name = "audit";
    public const string MigrationsHistoryTable = "__ef_migrations_history";
}
```

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/Persistence/AuditRecord.cs` — the append-only row. Fields: `Id` (`long`, identity), `ActorHandle` (`string`, max 256 — matches `ChangedByHandle` convention), `Action` (`string`, max 128), `EntityType` (`string`, max 128), `EntityId` (`string`, max 128 — stored as string so any module's id shape, e.g. `Guid` or composite key, fits without a generic-typing scheme), `MetadataJson` (`string?`, max 4000 — the `Metadata` dictionary serialized with `System.Text.Json`; nullable when no metadata supplied), `OccurredAtUtc` (`DateTimeOffset`). No `Update`/`Delete` methods on the entity — mutation only ever happens through `DbSet.Add`.

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/Persistence/AuditDbContext.cs` — copy `RoleManagementDbContext.cs`'s shape: primary-constructor `DbContext(DbContextOptions<AuditDbContext> options)`, one `DbSet<AuditRecord> AuditRecords`, `OnModelCreating` sets `HasDefaultSchema(AuditSchema.Name)`, table name `audit_record`, snake_case `HasColumnName` per field, and `HasMaxLength` matching the field list above. Add an index on `(EntityType, EntityId)` and on `OccurredAtUtc` to support the list view's filters (`entity type`, `date range`) without a full scan.

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/AuditRecorder.cs`:

```csharp
using Microsoft.Extensions.Logging;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;

namespace SquadCrm.Modules.Audit;

internal sealed class AuditRecorder(AuditDbContext dbContext, ILogger<AuditRecorder> logger) : IAuditRecorder
{
    public async Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.AuditRecords.Add(new AuditRecord
            {
                ActorHandle = request.ActorHandle,
                Action = request.Action,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                MetadataJson = request.Metadata is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(request.Metadata)
                    : null,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to record audit entry for {Action} on {EntityType}/{EntityId}.",
                request.Action, request.EntityType, request.EntityId);
        }
    }
}
```

The `catch` is the deliberate best-effort boundary described in `## Story Goal`: the caller's own business transaction has already committed by the time this runs; a failure here must never surface as a failure of the business operation.

**Permission wiring (decided — `PermissionRequirement`/`PermissionAuthorizationHandler` are `internal` to `SquadCrm.Modules.RoleManagement`, which has no `.Contracts` project, so `AuditModule` cannot reference them directly; the permission catalog is centrally owned by `RoleManagement`, and every other module follows that precedent — see `StaffIdentityModule.cs:124-125`'s `UsersViewPolicy`/`UsersManagePolicy` string constants, which reference RoleManagement's policies by name only, no project reference):**

- In `RoleManagement/Permissions.cs`, add `Permissions.AuditView = "audit.view"` to the `Permissions` class and `PermissionPolicies.AuditView = "permission:audit.view"` to the `PermissionPolicies` class (alongside the existing `RolesView`/`RolesManage`/`UsersView`/`UsersManage` pairs).
- In `RoleManagementModule.cs`'s `RegisterServices`, add `options.AddPolicy(PermissionPolicies.AuditView, policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.AuditView)));` to the existing `AddAuthorization` block (alongside the four existing `AddPolicy` calls).
- Add a `PermissionDefinition.HasData` seed row for `Permissions.AuditView` directly in `RoleManagementDbContext.cs`'s existing `HasData(...)` call (same file, same `OnModelCreating` block, additive alongside the four existing rows at lines 55-83), with `Module = "Audit"` (following the exact `Code`/`Name`/`Module`/`Description` shape already there). This row ships in a new `RoleManagement`-owned EF Core migration (e.g. `AddAuditViewPermission`), generated with `dotnet ef migrations add AddAuditViewPermission --project src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement --startup-project src/backend/src/Api/SquadCrm.Api --context SquadCrm.Modules.RoleManagement.Persistence.RoleManagementDbContext` — **`RoleManagement` owns its own schema/migration for this row, not `Audit`**; confirm the generated migration only contains the one `InsertData` and touches no other table.
- `AuditModule` itself never references `RoleManagement`'s implementation project. It only declares `private const string AuditViewPolicy = "permission:audit.view";` (a private string constant, matching `StaffIdentityModule.cs`'s `UsersViewPolicy` pattern exactly) and calls `.RequireAuthorization(AuditViewPolicy)` on its endpoints. `AuditModule.RegisterServices` does **not** call `AddAuthorization`/`AddPolicy` for this policy — that registration lives in `RoleManagementModule` alongside the rest of the permission catalog.

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/AuditModule.cs` — copy `RoleManagementModule.cs`'s shape:
- `RegisterServices`: `services.AddDbContext<AuditDbContext>(...)` with `MigrationsHistoryTable(AuditSchema.MigrationsHistoryTable, AuditSchema.Name)`; `services.AddScoped<IAuditRecorder, AuditRecorder>()`; `services.AddScoped<AuditQueryService>()` (task 3). No `AddAuthorization` call here — the `audit.view` policy is registered by `RoleManagementModule` per the permission-wiring note above.
- `MapEndpoints`: `GET /api/v1/audit-records` (paged list with `entityType`, `action`, `actorHandle`, `from`, `to` query filters) and `GET /api/v1/audit-records/{id:long}` (detail), both `.RequireAuthorization(AuditViewPolicy)` using the private `"permission:audit.view"` string constant declared on `AuditModule`.

### 3 — Query/list support

Create file: `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/AuditQueryService.cs` — `ListAsync(PaginationRequest pagination, string? entityType, string? action, string? actorHandle, DateTimeOffset? from, DateTimeOffset? to, CancellationToken)` returning `PagedResult<AuditRecord>` (copy `StaffUserService.ListAsync`'s shape at `StaffUserService.cs:76-97` for the `Skip`/`Take`/`CountAsync` pattern), and `GetAsync(long id, CancellationToken)` returning `AuditRecord?`. Grep `PagedResult` and `PaginationRequest` under `src/backend/src/BuildingBlocks` to confirm the exact existing type signatures before reusing them (do not redefine).

### 4 — EF Core migration

Run `dotnet ef migrations add CreateAuditSchema --project src/backend/src/Modules/Audit/SquadCrm.Modules.Audit --startup-project src/backend/src/Api/SquadCrm.Api --context SquadCrm.Modules.Audit.Persistence.AuditDbContext` (grep an existing `dotnet ef migrations add` invocation in `../manage-staff-users/18-story-crm-111-manage-staff-users.md` or CI config for the exact flags this repo uses — `--project`/`--startup-project`/`--context` names must match verbatim). Confirm the generated migration creates the `audit` schema and `audit.audit_record` table only, with no changes to any other module's schema.

### 5 — Wire into `RoleManagement`'s `AuthorizationBootstrapService`

File: `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/SquadCrm.Modules.RoleManagement.csproj` — add `<ProjectReference Include="../../Audit/SquadCrm.Modules.Audit.Contracts/SquadCrm.Modules.Audit.Contracts.csproj" />`.

File: `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/AuthorizationBootstrapService.cs` — add `IAuditRecorder auditRecorder` to the `AuthorizationBootstrapService(...)` primary-constructor parameter list (line 21-24). After the existing `dbContext.StaffSubjectRoles.Add(...)` block (lines 57-61) — i.e. only on the branch where the subject did not already have the role — and before the shared `await dbContext.SaveChangesAsync(cancellationToken)` at line 86, add:

```csharp
await auditRecorder.RecordAsync(
    new AuditRecordRequest(
        "bootstrap-tool",
        "role_assigned",
        "StaffSubjectRole",
        $"{subject.Id}:{role.Id}",
        new Dictionary<string, string> { ["roleCode"] = role.Code }),
    cancellationToken);
```

`AuthorizationBootstrapService` has no `ICurrentUserAccessor` dependency today (this bootstrap path runs from a CLI tool before any staff user is authenticated — it is what grants the *first* role — so there is no signed-in actor to read a handle from). Do not add `ICurrentUserAccessor` to this service just to populate `ActorHandle`; use the literal `"bootstrap-tool"` string, matching how `AuthorizationBootstrapService.cs:81` already passes `ChangedByHandle = null` for the same reason on the existing `PermissionChangeAuditEvent` write. `roleCode` (e.g. `"ADMIN"`) is a role identifier, never a secret. This call site sits before the shared `dbContext.SaveChangesAsync` success path used by the pre-existing `PermissionChangeAuditEvent` write at lines 74-84 for bootstrap permission grants — that write is transactional with the business write; only the new `IAuditRecorder.RecordAsync` call (a separate `DbContext`) is best-effort, per the transaction-boundary note above.

### 6 — Register `IAuditRecorder` in the `SquadCrm.RoleManagement.Bootstrap` CLI tool

File: `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/SquadCrm.RoleManagement.Bootstrap.csproj` — add a `<ProjectReference>` to `SquadCrm.Modules.Audit.Contracts.csproj` and, since this tool constructs its `DbContext`s directly rather than through `AuditModule.RegisterServices` (it never composes the `IModule` interface — see `BootstrapProgram.cs:37-41`), to `SquadCrm.Modules.Audit.csproj` as well so it can construct `AuditRecorder` directly.

File: `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs` — mirror the existing `services.AddDbContext<StaffIdentityDbContext>(...)` / `services.AddDbContext<RoleManagementDbContext>(...)` lines (37-38) with `services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString))`, and add `services.AddScoped<IAuditRecorder, AuditRecorder>();` alongside the existing `services.AddScoped<AuthorizationBootstrapService>();` (line 41). This is now a required change, not an open question — `AuthorizationBootstrapService`'s new constructor dependency (task 5) will fail to resolve at runtime in this tool without it, since this CLI process is the *only* caller of `BootstrapAsync`.

### 7 — Module registration in the API host

File: `src/backend/src/Api/SquadCrm.Api/Program.cs` — add `using SquadCrm.Modules.Audit;` near the other module `using`s, and add `new AuditModule(),` to the module list before `new StaffIdentityModule()` (currently lines 153-154). The CLI tool's own registration is handled separately in task 6 — it does not go through `Program.cs`.

### 8 — Architecture-test registration (mandatory — these test files MUST be edited)

The repo's architecture tests are reflection-based and only check assemblies they are explicitly told about; a new module is invisible to them until registered. This is not "zero changes to the test files" (an earlier draft of this plan said that; it was wrong) — these two files must be edited:

- `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` — add `using SquadCrm.Modules.Audit;` and `using SquadCrm.Modules.Audit.Contracts;`, then add `public static Assembly Audit { get; } = typeof(AuditModule).Assembly;` and `public static Assembly AuditContracts { get; } = typeof(IAuditRecorder).Assembly;` (mirroring the `StaffIdentity`/`StaffIdentityContracts` pair at lines 54-56), and add both to the `All` list (lines 69-86). Without this, `EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace`, `Modules_MustNotDependOnAnotherModulesPersistenceNamespace`, `ModuleContracts_MustNotDependOnEfCoreOrNpgsql`, and `ContractsAssemblies_MustNotDependOnBuildingBlocks` silently skip the new module and pass for the wrong reason.
- `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrm.ArchitectureTests.csproj` — add `<ProjectReference>` entries for `SquadCrm.Modules.Audit.csproj` and `SquadCrm.Modules.Audit.Contracts.csproj` (mirroring the existing `StaffIdentity`/`StaffIdentity.Contracts` pair), so the assembly references above resolve.

Also add, as new files (not edits to existing tests):

- `src/backend/tests/SquadCrm.Persistence.IntegrationTests/SchemaOwnershipTests.cs` — add a new `AuditSchemaOwnershipTests` class in this file, following the `RoleManagementSchemaOwnershipTests` precedent already in the file (`:130-134` onward: `ModuleSchema = "audit"`, `HistoryTable = "__ef_migrations_history"`, the same `Schema_ExistsForTheOwningModule`/`MigrationHistory_LivesInModuleSchema`-shaped assertions against `information_schema`).
- `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit/Persistence/AuditDbContextFactory.cs` — the design-time factory, copying `RoleManagementDbContextFactory.cs`'s exact shape (`IDesignTimeDbContextFactory<AuditDbContext>`, `ReadPostgresOptions().BuildConnectionString()`, `MigrationsHistoryTable(AuditSchema.MigrationsHistoryTable, AuditSchema.Name)`) — needed so `dotnet ef migrations add` (task 4) and any CI migration-check tooling can construct `AuditDbContext` at design time without a running host.

---

## Frontend Tasks

### 9 — Audit API client

Create file: `src/frontend/projects/agent-crm/src/app/audit/audit.service.ts` — copy `staff-users.service.ts`'s shape (88 lines): `AuditRecord` interface (`id: string`/`number` — match whatever the backend DTO serializes `long Id` as; `actorHandle`, `action`, `entityType`, `entityId`, `metadata: Record<string, string> | null`, `occurredAtUtc: string`), `list(page, pageSize, filters)` calling `GET /api/v1/audit-records` with query params for `entityType`/`action`/`actorHandle`/`from`/`to`, and `get(id)` calling `GET /api/v1/audit-records/{id}`.

### 10 — Audit list screen

Create files: `src/frontend/projects/agent-crm/src/app/audit/audit-list.ts`, `audit-list.html`, `audit-list.scss` — copy `staff-user-list.ts`'s shape (72 lines): `TableLazyLoadEvent`/`onLazyLoad`, `signal`-based `auditRecords`/`totalRecords`/`loading` state, plus filter `signal`s for `entityType`/`action`/`actorHandle`/date range using PrimeNG `Calendar`/`Dropdown`/`InputText` (grep existing PrimeNG module imports already used elsewhere in `staff-user-list.ts`/`role-list.ts` before importing a new PrimeNG module not already used in this app, to stay consistent with what is already vetted). Row click navigates to the detail route (`RouterLink` to `/audit/:id`, same as `staff-user-list.html`'s pattern for its own detail/edit links).

### 11 — Audit detail screen

Create files: `src/frontend/projects/agent-crm/src/app/audit/audit-detail.ts`, `audit-detail.html`, `audit-detail.scss` — a read-only detail view (no form, no edit) showing actor, action, entity type/id, timestamp, and metadata as a simple key/value list (PrimeNG `Card`/plain table — grep for an existing read-only detail screen precedent, e.g. under `roles/` or `staff-users/`, before choosing a layout not already established).

### 12 — Translations

Create file: `src/frontend/projects/agent-crm/src/app/audit/audit-translations.ts` — copy `staff-user-translations.ts`'s `TranslationResources` shape with `en`/`ar` keys under `audit.*` (`audit.title`, `audit.fields.actor`, `audit.fields.action`, `audit.fields.entityType`, `audit.fields.entityId`, `audit.fields.occurredAt`, `audit.fields.metadata`, `audit.empty`, `audit.filters.*`). Provide real Arabic translations, not machine-transliterated placeholders — match the tone/register already used in `STAFF_USER_TRANSLATIONS`'s `ar` block.

### 13 — Routing and registration

File: `src/frontend/projects/agent-crm/src/app/app.routes.ts` — add, inside the same `children` array as the existing `roles`/`staff-users` routes (after line 59):

```typescript
{
  path: 'audit',
  canActivate: [requirePermission('audit.view')],
  loadComponent: () => import('./audit/audit-list').then((m) => m.AuditList),
},
{
  path: 'audit/:id',
  canActivate: [requirePermission('audit.view')],
  loadComponent: () => import('./audit/audit-detail').then((m) => m.AuditDetail),
},
```

File: `src/frontend/projects/agent-crm/src/app/app.config.ts` — add `import { AUDIT_TRANSLATIONS } from './audit/audit-translations';` (near line 22) and `provideTranslations(AUDIT_TRANSLATIONS),` (near line 37).

Grep the shell/nav component (wherever `roles`/`staff-users` nav links are registered, e.g. under `shell/agent-shell*`) for how sidebar/nav entries are added, and add an "Audit" nav entry gated the same way as the existing `roles`/`staff-users` entries — do not invent a new nav-gating mechanism.

---

## Edge Cases & Failure Modes

- **Audit write fails after the business write commits** (e.g. `AuditDbContext`'s connection is briefly down): `AuditRecorder.RecordAsync`'s `catch` swallows the exception and logs it (task 2). The business operation (`AuthorizationBootstrapService.BootstrapAsync`'s role grant) still returns success to its caller. This is a deliberate trade-off — the new `AuditRecord` write is genuinely best-effort, unlike the four existing transactional audit classes (see the transaction-boundary note above and `## Known Limitations & Out of Scope`) — document this trade-off in a code comment on `AuditRecorder.RecordAsync` itself so a future reader does not "fix" it into a distributed transaction.
- **`Metadata` accidentally carries a secret**: `AuditRecordRequest.Metadata` only accepts `IReadOnlyDictionary<string, string>` built explicitly by the one call site (task 5) — no reflection-based serialization of an arbitrary object. Enforce by code review: only the exact field named in task 5 (`roleCode`) is ever passed; never pass a raw request DTO or entity.
- **Empty/whitespace filter query params** on `GET /api/v1/audit-records` (e.g. `entityType=` or `actorHandle=%20`): treat as "no filter", mirroring `StaffUserService.ListAsync`'s `string.IsNullOrWhiteSpace(search)` check at `StaffUserService.cs:82`.
- **Unknown `id` on `GET /api/v1/audit-records/{id}`**: return 404 with the same `Results.Problem(...)` + `extensions["code"]` shape as `RoleManagementModule.cs`'s `NotFoundProblem()` (lines 230-233), e.g. `"audit.not_found"`.
- **Concurrent audit writes from parallel requests**: each `RecordAsync` call opens its own `AuditDbContext` instance (scoped per-request/per-process DI) and does a plain `Add`+`SaveChangesAsync` — no shared mutable state, no expected concurrency conflict (append-only, no updates).
- **Migration ordering**: the new `audit` schema migration (task 4) creates only the `audit` schema and `audit.audit_record` table. The separate `Permissions.AuditView` seed row (task 2) lands in its own `RoleManagement`-owned migration (`AddAuditViewPermission`) and must not be combined with the `Audit` schema migration — verify each generated migration file only contains what its own task describes.
- **`SquadCrm.RoleManagement.Bootstrap` tool** (tasks 5-6): this CLI tool is the only caller of `AuthorizationBootstrapService.BootstrapAsync` and builds its own `ServiceCollection` rather than going through `Program.cs`'s module list. It **must** register `IAuditRecorder` (task 6) — this is decided, not conditional — otherwise `AuthorizationBootstrapService`'s new constructor dependency (task 5) fails DI resolution at runtime and the tool cannot run at all. Confirm by running the tool end-to-end (or a test exercising its DI container) before considering tasks 5-6 complete.
- **`ICurrentUserAccessor` may be unavailable in the bootstrap path**: `AuthorizationBootstrapService` has no `ICurrentUserAccessor` dependency today and this CLI tool typically runs before any staff user is authenticated. Per task 5, use a literal `"bootstrap-tool"` `ActorHandle` rather than introducing a new dependency this service does not otherwise need.

---

## Test Plan

1. **Unit** — `src/backend/tests/SquadCrm.UnitTests/` — add `AuditRecorderTests.cs` (grep the existing unit-test project's `PostgresTestDatabase` usage, or an in-memory/mock pattern already used for a similarly small service, before choosing a test double for `AuditDbContext`): assert `RecordAsync` swallows an exception thrown by a failing `SaveChangesAsync` and logs it (verify via a mock/fake `ILogger`), and never lets the exception propagate.
2. **Persistence integration** — `src/backend/tests/SquadCrm.Persistence.IntegrationTests/AuditTests.cs` (new file, copy `RoleManagementTests.cs`'s `[Collection(PostgresTestDatabase.CollectionName)]` shape): `RecordAsync_Succeeds_AndPersistsOneAuditRecord`, `RecordAsync_WithMetadata_PersistsSerializedMetadataJson`, `RecordAsync_NeverExposesUpdateOrDeleteApi` (compile-time/reflection assertion that `AuditRecord` has no setter path reachable outside `Add`, if that is feasible given the entity shape — otherwise document as N/A and rely on code review). Add a `PostgresTestDatabase.CreateAuditContext()` factory method mirroring `CreateRoleManagementContext()` (grep `PostgresTestDatabase.cs` for the exact existing factory pattern first).
3. **Integration proof for wiring** — extend `AuditTests.cs` or add a new test in `SquadCrm.Persistence.IntegrationTests` covering `RoleManagement`: `AuthorizationBootstrapService.BootstrapAsync produces exactly one AuditRecord with EntityType "StaffSubjectRole" and the roleCode metadata, when it newly assigns a role` (mirrors `RoleManagementTests.cs:14-32`'s `Create_Succeeds_AndProducesOneCreatedAuditEvent` pattern, but asserting against `AuditDbContext.AuditRecords` instead of `RoleAuditEvents`), and a companion case asserting `BootstrapAsync` produces **no** `AuditRecord` when the subject already had the role (the `if (!await dbContext.StaffSubjectRoles.AnyAsync(...))` branch at `AuthorizationBootstrapService.cs:54-62` is skipped). Explicitly assert `StaffUserService` and `PermissionService` still only write to `AuthenticationEvent`/`PermissionChangeAuditEvent` and never call `IAuditRecorder` — a regression test proving the user's decision not to dual-write is upheld.
4. **API authorization** — `src/backend/tests/SquadCrm.Api.Tests/AuditEndpointsAuthorizationTests.cs` (new file, copy `RoleEndpointsAuthorizationTests.cs`'s shape): `GET /api/v1/audit-records` returns 401 unauthenticated, 403 without `audit.view`, 200 with `audit.view`; same for the `/{id}` detail endpoint including a 404 case for an unknown id.
5. **Architecture tests** — these files ARE edited by task 8, not left untouched: `SquadCrmAssemblies.cs` and `SquadCrm.ArchitectureTests.csproj` register the new `Audit`/`Audit.Contracts` assemblies. After that registration, run `ModuleProjectDependencyRulesTests.Modules_MayReferenceOtherModulesOnlyThroughContractsProjects`, `EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace`, `Modules_MustNotDependOnAnotherModulesPersistenceNamespace`, `ModuleContracts_MustNotDependOnEfCoreOrNpgsql`, and `ContractsAssemblies_MustNotDependOnBuildingBlocks` — they must pass, proving the new module respects the existing boundary rules once it is actually visible to them. Also run the new `AuditSchemaOwnershipTests` (task 8).
6. **Frontend unit** — `audit-list.spec.ts`, `audit-detail.spec.ts`, `audit.service.spec.ts` (copy `staff-user-list.ts`'s sibling spec file pattern — grep for `staff-user-list.spec.ts` if it exists, otherwise `role-list.spec.ts`, for the exact TestBed setup used in this codebase).
7. **Frontend permission-guard** — extend or copy `permission.guard.spec.ts`'s pattern to assert the `/audit` and `/audit/:id` routes are guarded by `audit.view`.

---

## Migration / Rollback

- Two separate, independent EF Core migrations are added: (1) the `Audit`-owned migration (task 4) creates only the new `audit` schema and `audit.audit_record` table — it does not alter any existing table; (2) the `RoleManagement`-owned migration `AddAuditViewPermission` (task 2) adds only one `InsertData` row (`Permissions.AuditView`) to the existing `role_management.permission_definition` table — it does not touch `audit` or `staff_identity`.
- **Rollback:** for the `Audit` migration, `dotnet ef database update <previous-migration-name> --project src/backend/src/Modules/Audit/SquadCrm.Modules.Audit --startup-project src/backend/src/Api/SquadCrm.Api --context SquadCrm.Modules.Audit.Persistence.AuditDbContext`. For the `RoleManagement` migration, the equivalent command scoped with `--project src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement --context SquadCrm.Modules.RoleManagement.Persistence.RoleManagementDbContext`. The two are independent and can be rolled back in either order.
- **Half-applied state:** if the `audit` schema migration succeeds but a later step in the same deploy fails, the schema/table exist but nothing writes to them yet (the call site in task 5 is a separate, independently deployable code change) — this is safe to leave in place; no partial-write corruption is possible because nothing references the table until task 5 also ships. Similarly, if `AddAuditViewPermission` lands before `AuditModule`/its endpoints are deployed, the permission simply exists in the catalog unassigned — harmless.

---

## Verification Steps

1. **Backend format:** `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` — no diff.
2. **Backend builds:** `dotnet build src/backend/SquadCrm.sln --no-restore` — no errors.
3. **Backend tests:** `dotnet test src/backend/SquadCrm.sln --no-restore --no-build` — all pass, including the new `AuditTests.cs`, `AuditEndpointsAuthorizationTests.cs`, `AuditRecorderTests.cs`, `AuditSchemaOwnershipTests`, and `SquadCrm.ArchitectureTests` (updated per task 8 to register the new module, then passing against it).
4. **Frontend format:** `npm run format:check` (in `src/frontend`) — no diff.
5. **Frontend lint:** `npm run lint` (in `src/frontend`) — no errors.
6. **Frontend tests:** `npm run test` (in `src/frontend`) — all pass, including new `audit-*.spec.ts` files.
7. **Frontend production build:** `ng build agent-crm --configuration=production` (in `src/frontend`) — succeeds within existing budget limits (mirrors `../manage-staff-users/18-story-crm-111-manage-staff-users.md:142`).
8. **Regression:** re-run `StaffUserEndpointsAuthorizationTests` and `RoleEndpointsAuthorizationTests` (existing, unmodified expected behavior — neither `StaffUserService` nor `PermissionService` gained an `IAuditRecorder` dependency) and `RoleManagementTests.cs`/any existing `StaffIdentity` persistence integration tests — confirm `AuthorizationBootstrapService`'s new `IAuditRecorder` constructor dependency did not change any existing assertion's outcome, and run the `SquadCrm.RoleManagement.Bootstrap` CLI tool (or an integration test exercising its DI container) end-to-end to confirm it still resolves and runs after task 6's registration change.

---

## Done Criteria

- [ ] `IAuditRecorder`/`AuditRecordRequest` exist in `SquadCrm.Modules.Audit.Contracts`; `AuditRecorder`/`AuditRecord`/`AuditDbContext` exist in `SquadCrm.Modules.Audit`, in their own `audit` schema.
- [ ] No `Update`/`Delete` code path exists for `AuditRecord` anywhere in the codebase or its API.
- [ ] `GET /api/v1/audit-records` (list, with `entityType`/`action`/`actorHandle`/date-range filters) and `GET /api/v1/audit-records/{id}` (detail) exist, both gated by `PermissionPolicies.AuditView`.
- [ ] `Permissions.AuditView`/`PermissionPolicies.AuditView` are added to `RoleManagement`'s permission catalog (`Permissions.cs`, `RoleManagementModule.cs`'s `AddPolicy` block, and a `PermissionDefinition.HasData` row seeded via a `RoleManagement`-owned migration), assignable through the existing role/permission UI (CRM-113), with no new authorization mechanism invented. `AuditModule` references this policy only by its `"permission:audit.view"` string constant and has no project reference to `SquadCrm.Modules.RoleManagement`.
- [ ] No `Metadata` value passed to `IAuditRecorder.RecordAsync` anywhere in the codebase contains a password, token, OTP, or secret — verified by reading the one call site added in task 5.
- [ ] `AuthorizationBootstrapService.BootstrapAsync` calls `IAuditRecorder.RecordAsync` exactly once when it newly assigns a role to a staff subject, and zero times when the subject already had the role — proven by the integration tests in Test Plan item 3.
- [ ] `StaffUserService` and `PermissionService.ReplaceAsync` are confirmed to make **zero** calls to `IAuditRecorder` — proven by the regression test in Test Plan item 3, upholding the user's decision not to dual-write.
- [ ] `SquadCrm.RoleManagement.Bootstrap` (the CLI tool) registers `IAuditRecorder` and runs successfully end-to-end after `AuthorizationBootstrapService` gains its new constructor dependency (task 6, Verification Steps item 8).
- [ ] Audit list/detail screens exist under `src/frontend/projects/agent-crm/src/app/audit/`, reuse the existing shell/responsive layout, and have complete EN/AR translations with no missing keys.
- [ ] `RoleAuditEvent`, `PermissionChangeAuditEvent`, `StaffRoleAssignmentAuditEvent`, and `AuthenticationEvent` are unmodified — confirmed by `git diff` touching none of `Persistence/RoleAuditEvent.cs`, `Persistence/PermissionChangeAuditEvent.cs`, `Persistence/StaffRoleAssignmentAuditEvent.cs`, or `StaffIdentity`'s `AuthenticationEvent` entity/table. `RoleService.cs` and `StaffRoleAssignmentService.cs` are also unmodified.
- [ ] `SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` and `SquadCrm.ArchitectureTests.csproj` are edited to register the new `Audit`/`Audit.Contracts` assemblies (task 8) — this is a required change, not an exemption from the architecture tests.
- [ ] All items in `## Verification Steps` pass.
