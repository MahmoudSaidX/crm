# Story 22 — Support First Administrator Bootstrap on Fresh Environment (Story: CRM-206)

## Prerequisites

- StaffIdentity bootstrap (`src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap`) must have already created the target staff account — this story never creates a `StaffUser`.
- Story 17 (`../configure-role-permissions/17-story-crm-113.md`) established the `PermissionDefinition` catalog table, `role_permission` grants, and `PermissionService`/`PermissionChangeAuditEvent` — this story reads that same catalog table, it does not add a new one.
- Story 14 (`../manage-roles/14-story-manage-roles.md`) established `Role`/`RoleAuditEvent` and the `RoleService.Normalize` convention this story reuses.

---

## Story Goal

Fix the fresh-environment bootstrap deadlock: on a genuinely fresh database there are no `Role` rows, and both creating a role and granting permissions require `roles.manage`, which no subject can hold yet. Extend `AuthorizationBootstrapService` (used only by `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap`) so a single explicit operator invocation can, in one step:

1. Validate the target `StaffIdentity` subject (by email) exists and is active — fail clearly, with no writes, when it does not.
2. Create the named role if it does not already exist (active by default); reuse it if it does exist and is active; fail clearly (no partial writes) if it exists but is inactive.
3. Grant the role the **complete current `PermissionDefinition` catalog** — every row in `role_management.permission_definition` at the moment the tool runs — never a hardcoded subset.
4. Assign the staff subject to the role, reusing the existing `StaffSubjectRole` assignment path.
5. Be idempotent: re-running with the same arguments adds no duplicate role, grant, or assignment row, and performs no redundant writes.

**Permission-catalog Decision Gate — resolved, no gate hit.** `role_management.permission_definition` (`Persistence/PermissionDefinition.cs`, `RoleManagementDbContext.cs:47-133`) is already the canonical registered-permission-catalog table: every permission added to the product (roles, users, audit, departments, branches, configuration, branding, customers — see the `Add*Permissions` migrations under `Persistence/Migrations/`) is inserted into this same table via `migrationBuilder.InsertData(schema: "role_management", table: "permission_definition", ...)`, and `PermissionService.GetCatalogAsync` (`PermissionService.cs:25-46`) already reads it as the source of truth for the catalog exposed at `GET /api/v1/permissions`. The full current catalog is a plain `dbContext.PermissionDefinitions.Select(p => p.Code)` query — no shared contract changes needed. `Permissions.Bootstrap` (`Permissions.cs:20`, the hardcoded four-code list: `roles.view`, `roles.manage`, `users.view`, `users.manage`) is the thing being replaced, not a constraint to preserve.

**Not in scope**: any HTTP endpoint, startup auto-provisioning, hardcoded admin credentials, StaffIdentity password-bootstrap changes, role-management UI, a generic CLI framework, or seeding a permanent administrator through EF migrations.

---

## Context — Read These Files First

1. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/AuthorizationBootstrapService.cs` (full file, 105 lines) — the service being extended. Today `BootstrapAsync(subjectEmail, roleCode, cancellationToken)` requires the role to already exist and be active (`RoleNotFound`/`RoleInactive` at lines 45-53) and grants only `Permissions.Bootstrap` (lines 67-71, 20 in `Permissions.cs`). This is the deadlock to remove.
2. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/RoleService.cs:33-74` (`CreateAsync`) and `:191-195` (`Normalize`, `IsUniqueViolation`) — the exact pattern to mirror for constructing a new `Role` and catching a concurrent-insert unique-violation race (SQLSTATE `23505`). Do **not** inject `RoleService` itself into `AuthorizationBootstrapService` — `RoleService` requires `ICurrentUserAccessor`, which the console-tool `ServiceCollection` in `BootstrapProgram.cs` (Tools project) does not register; keep `AuthorizationBootstrapService` self-contained against `RoleManagementDbContext`, exactly as it already is.
3. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/RoleManagementDbContext.cs:47-53` — `PermissionDefinition` entity mapping (`HasKey(p => p.Code)`) confirming `dbContext.PermissionDefinitions.Select(p => p.Code)` is a valid, indexed, cheap query for "every currently registered permission code".
4. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Permissions.cs` (21 lines) — remove the `Bootstrap` static list (line 20); every other constant is unaffected.
5. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/Role.cs`, `RolePermission.cs`, `StaffSubjectRole.cs`, `RoleAuditEvent.cs`, `PermissionChangeAuditEvent.cs` — entity shapes already used by `AuthorizationBootstrapService`; `Role` creation must set every required field the same way `RoleService.CreateAsync` does (`Id`, `Name`, `NormalizedName`, `Code`, `NormalizedCode`, `Description`, `IsActive = true`, `CreatedAtUtc`, `UpdatedAtUtc`).
6. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Contracts/StaffSubjectReference.cs` — `IStaffSubjectReferenceReader.FindByNormalizedEmailAsync` contract already injected and used unchanged (lines 32-42 of the service).
7. `src/backend/src/Modules/Audit/SquadCrm.Modules.Audit.Contracts` (`IAuditRecorder`, `AuditRecordRequest`) — the cross-cutting audit call at `AuthorizationBootstrapService.cs:91-101` (`"bootstrap-tool"`, `"role_assigned"`) fires only on a **new** assignment; keep this behavior for the assignment step, and do **not** add a second `IAuditRecorder` call for role creation or permission grants — those already get their own in-module audit rows (`RoleAuditEvent`, `PermissionChangeAuditEvent`), matching the existing split (module-local audit for module-local writes, `IAuditRecorder` only for the cross-cutting assignment fact).
8. `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs` (full file, 85 lines) — CLI entry point. `TryReadOption` (lines 73-84) parses `--subject-email`/`--role-code`; the failure-to-message `switch` (lines 58-65) must drop the `RoleNotFound` case (no longer reachable) and gains a new case for the role-creation race failure.
9. `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/SquadCrm.RoleManagement.Bootstrap.csproj` — no new `ProjectReference` needed; `RoleManagementDbContext` is already referenced.
10. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PermissionManagementTests.cs:90-115` (`Bootstrap_IsIdempotent_AndRejectsInvalidOrInactiveSubjectsWithoutWrites`) and `:116-120` (`CreateRoleAsync` helper) — every `BootstrapAsync(...)` call here passes exactly 3 positional args (`subjectEmail`, `roleCode`, `cancellationToken`); all must be updated for the new signature. Line 99 asserts `Permissions.Bootstrap.Count` — replace with a catalog-derived count.
11. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/AuditTests.cs:53-107` (`Bootstrap_NewlyAssignedRole_ProducesExactlyOneAuditRecord`, `Bootstrap_SubjectAlreadyHasRole_ProducesNoAuditRecord`) — same 3-positional-arg call sites (lines 66-67, 89-90 twice, 100-101) to update; these tests must keep passing unmodified in behavior (role already exists and is active in both, created by the local `CreateRoleAsync` helper).
12. `README.md:339-355` ("Bootstrap the first role administrator") — currently instructs the operator to "First create an active staff subject and an active global role through the normal CRM-110/CRM-112 paths" before running the tool. That is exactly the deadlock this story removes (creating a role through the normal path requires `roles.manage`, which does not yet exist). Rewrite this section to document the new self-contained sequence and drop the CRM-110/CRM-112 role-creation prerequisite.

---

## Backend Tasks

### 1 — Derive the full permission catalog and allow role creation

**File: `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/AuthorizationBootstrapService.cs`**

Replace the failure enum, add a role-creation path, and replace the `Permissions.Bootstrap` grant with the full catalog:

```csharp
public enum AuthorizationBootstrapFailure
{
    None,
    SubjectNotFound,
    SubjectInactive,
    RoleInactive,
    RoleConflict,
}
```

(`RoleNotFound` is removed — a missing role is no longer a failure, it is created. `RoleConflict` is new — a concurrent bootstrap run losing a unique-index race on role name/code.)

In `BootstrapAsync`, after the subject checks (lines 32-42, unchanged) and before the existing role lookup, extend the role lookup to create-if-missing:

```csharp
string normalizedRoleCode = RoleService.Normalize(roleCode);
Role? role = await dbContext.Roles.SingleOrDefaultAsync(
    item => item.NormalizedCode == normalizedRoleCode, cancellationToken);
bool roleCreated = false;
if (role is null)
{
    DateTimeOffset now = DateTimeOffset.UtcNow;
    string effectiveName = string.IsNullOrWhiteSpace(roleName) ? roleCode : roleName;
    role = new Role
    {
        Id = Guid.NewGuid(),
        Name = effectiveName.Trim(),
        NormalizedName = RoleService.Normalize(effectiveName),
        Code = roleCode.Trim(),
        NormalizedCode = normalizedRoleCode,
        Description = "Bootstrapped by the RoleManagement.Bootstrap operator tool.",
        IsActive = true,
        CreatedAtUtc = now,
        UpdatedAtUtc = now,
    };
    dbContext.Roles.Add(role);
    dbContext.RoleAuditEvents.Add(new RoleAuditEvent
    {
        RoleId = role.Id,
        EventType = "created",
        ChangedByHandle = null,
        OccurredAtUtc = now,
    });
    roleCreated = true;
}
else if (!role.IsActive)
{
    return new(AuthorizationBootstrapFailure.RoleInactive);
}
```

Change the method signature to `BootstrapAsync(string subjectEmail, string roleCode, string? roleName, CancellationToken cancellationToken)` — `roleName` is required at the call site (nullable, no default) so every caller states its intent explicitly; when null or whitespace, `roleCode` is reused as the display name (mirrors the CLI's existing single `--role-code` argument staying sufficient for the common case).

Replace the permission-grant block (original lines 67-77) with a catalog-derived version:

```csharp
string[] catalogCodes = await dbContext.PermissionDefinitions
    .Select(item => item.Code)
    .ToArrayAsync(cancellationToken);
string[] existingGrants = await dbContext.RolePermissions
    .Where(item => item.RoleId == role.Id)
    .Select(item => item.PermissionCode)
    .ToArrayAsync(cancellationToken);
string[] missingPermissions = catalogCodes.Except(existingGrants, StringComparer.Ordinal).ToArray();
foreach (string code in missingPermissions)
{
    dbContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionCode = code });
}

if (missingPermissions.Length > 0)
{
    dbContext.PermissionChangeAuditEvents.Add(new PermissionChangeAuditEvent
    {
        RoleId = role.Id,
        EventType = "bootstrap_permissions_granted",
        PermissionCodes = string.Join(',', missingPermissions.OrderBy(code => code, StringComparer.Ordinal)),
        ChangedByHandle = null,
        OccurredAtUtc = DateTimeOffset.UtcNow,
    });
}
```

Wrap the final `await dbContext.SaveChangesAsync(cancellationToken);` in the same unique-violation catch pattern as `RoleService.CreateAsync` (`RoleService.cs:62-71`), reusing the same SQLSTATE constant (`"23505"`):

```csharp
try
{
    await dbContext.SaveChangesAsync(cancellationToken);
}
catch (DbUpdateException exception) when (IsUniqueViolation(exception))
{
    return new(AuthorizationBootstrapFailure.RoleConflict);
}
```

Add the private `IsUniqueViolation` helper (copy from `RoleService.cs:193-195`, same `Npgsql.PostgresException`/`SqlState` check) and the `PostgresUniqueViolationSqlState = "23505"` constant into `AuthorizationBootstrapService`. Add `using Npgsql;` and `using Microsoft.EntityFrameworkCore;`(already present) to the file.

Keep the existing role-assignment block (original lines 56-65) and the `IAuditRecorder` call (lines 91-101) unchanged in placement and content — only the surrounding failure/grant logic changes. The `roleCreated` local is read only if you choose to log a distinct message; it is not required for correctness (idempotency already holds because the role lookup + `Except` are re-evaluated every run).

### 2 — Update the CLI entry point

**File: `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs`**

Add an optional `--role-name` option alongside the existing required `--subject-email`/`--role-code` pair. Change the argument-count/parsing gate (lines 20-28):

```csharp
if (args.Length is not (4 or 6)
    || !TryReadOption(args, "--subject-email", out string? subjectEmail)
    || !TryReadOption(args, "--role-code", out string? roleCode)
    || string.IsNullOrWhiteSpace(subjectEmail)
    || string.IsNullOrWhiteSpace(roleCode))
{
    Console.Error.WriteLine(
        "Usage: --subject-email <existing-email> --role-code <role-code> [--role-name <role-name>]");
    return 2;
}

TryReadOption(args, "--role-name", out string? roleName);
```

Update the call at line 52-54 to pass `roleName`:

```csharp
AuthorizationBootstrapResult result = await scope.ServiceProvider
    .GetRequiredService<AuthorizationBootstrapService>()
    .BootstrapAsync(subjectEmail, roleCode, roleName, cancellationToken);
```

Update the failure-message `switch` (lines 58-65): drop the `AuthorizationBootstrapFailure.RoleNotFound` arm (no longer exists) and add:

```csharp
AuthorizationBootstrapFailure.RoleConflict =>
    "The role could not be created due to a concurrent bootstrap run; re-run the command.",
```

`TryReadOption` (lines 73-84) already tolerates an absent `--role-name` (returns `false`, `value` stays `null`) without needing changes — its `args.Count(item => item == name) != 1` guard only rejects a *duplicated* flag, and the initial length gate above (`4 or 6`) already restricts total argument count so a missing optional flag is consistent (4 args = no `--role-name`, 6 args = with it).

### 2a — Discovered during implementation: pre-existing CLI wiring gap (required completion fix)

**File: `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap/BootstrapProgram.cs`**

Manual end-to-end verification (Verification Step 5) surfaced that the CLI's
composition, unrelated to Tasks 1-3 above, never actually worked: it built a
plain `new ConfigurationBuilder().AddEnvironmentVariables().Build()` and called
`GetSquadCrmPostgresConnectionString()` directly, without ever running
`AddSquadCrmPostgres()` first (the only method that reads `POSTGRES_*` and
publishes `ConnectionStrings:SquadCrmPostgres` — see
`PostgresConfiguration.cs:106-134`), so the connection string was never
derived and every invocation failed with "PostgreSQL configuration is invalid
or incomplete." Separately, its bare `ServiceCollection` never registered
logging, so once the connection-string call was patched around, DI resolution
of `AuditRecorder` (which requires `ILogger<AuditRecorder>`) also failed.

Since the tool's one observable job — provisioning the first administrator on
a fresh environment — was unreachable through the supported CLI, this is
treated as a required completion fix for this story rather than a separate
ticket. The fix reuses the exact pattern the API composition root already
uses (`src/Api/SquadCrm.Api/Program.cs:130,133`): build a
`HostApplicationBuilder` (`Host.CreateApplicationBuilder()`, which registers
default console logging), call `builder.AddSquadCrmPostgres()`, then read
`builder.Configuration.GetSquadCrmPostgresConnectionString()`, and register
the tool's `DbContext`/service types on `builder.Services` instead of a bare
`ServiceCollection`. No new configuration system, no direct Postgres access,
no change to `AuthorizationBootstrapService`'s business logic, arguments, or
authorization behavior — only the CLI's own infrastructure wiring.

### 3 — Remove the hardcoded bootstrap permission list

**File: `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Permissions.cs`**

Delete line 20 (`public static readonly IReadOnlyList<string> Bootstrap = [...]`). No other file outside this story's Task 1 and the tests in Task 4 references `Permissions.Bootstrap` (confirmed by repo-wide grep).

### 4 — Update existing tests for the new signature and catalog behavior

**File: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PermissionManagementTests.cs`**

- Line 97: `Assert.True((await valid.BootstrapAsync("agent@example.test", role.Code, CancellationToken.None)).Succeeded);` → add `roleName: null` as the third argument (twice, lines 97-98).
- Line 99: `Assert.Equal(Permissions.Bootstrap.Count, await context.RolePermissions.CountAsync(item => item.RoleId == role.Id));` → replace `Permissions.Bootstrap.Count` with `await context.PermissionDefinitions.CountAsync()` (the full catalog size at test-run time — the assertion still holds because `CreateRoleAsync` produces a brand-new role with zero prior grants).
- Lines 106-109: the two `missing`/`inactive` `BootstrapAsync(...)` calls also need the extra `null` argument.

**File: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/AuditTests.cs`**

- Line 66-67 (`Bootstrap_NewlyAssignedRole_ProducesExactlyOneAuditRecord`) and lines 89-90, 100-101 (`Bootstrap_SubjectAlreadyHasRole_ProducesNoAuditRecord`) — add `roleName: null` to each `BootstrapAsync(...)` call. No other assertion in these two tests changes: both use a role already created via the local `CreateRoleAsync` helper, so the role-creation path is not exercised here (covered by the new tests in Task 5).

---

## Edge Cases & Failure Modes

- **Fresh database, role does not exist** — `BootstrapAsync` creates it (`IsActive = true` by construction), grants the full catalog, assigns the subject; enforced by the new create-branch in `AuthorizationBootstrapService.BootstrapAsync`. This is the story's primary scenario (Acceptance Criterion B).
- **Missing staff subject** — unchanged: `SubjectNotFound` returned before any `Role`/`RolePermission`/`StaffSubjectRole` write is staged, so no partial state is possible (Acceptance Criterion A). Enforced at `AuthorizationBootstrapService.cs:32-37` (unchanged line numbers before the role block).
- **Role exists and is active** — reused as-is; only missing permission grants and a missing subject assignment are added. No duplicate `Role` row, no duplicate `RoleAuditEvent`.
- **Role exists but is inactive** — fails with `RoleInactive` and stages no writes (the failure is returned before any `dbContext.Add`/`Remove` call in that run). The tool does **not** silently reactivate a role an operator may have deliberately deactivated — that is a deliberate, conservative behavior preserved from the original implementation, not a new policy decision.
- **Re-running with identical arguments (Acceptance Criterion C)** — role lookup finds the existing row (no second create), `catalogCodes.Except(existingGrants, ...)` yields an empty array (no duplicate `RolePermission` rows, no second `PermissionChangeAuditEvent`), and the `StaffSubjectRoles.AnyAsync` check (unchanged) prevents a duplicate assignment row and a duplicate `IAuditRecorder` call.
- **Concurrent bootstrap runs targeting the same new role code** — the `NormalizedCode`/`NormalizedName` unique indexes (`RoleManagementDbContext.cs:26,29`) are the real guard; the losing `SaveChangesAsync` throws a Postgres `23505` violation, caught and reported as `RoleConflict` rather than crashing with an unhandled `DbUpdateException` or leaking a stack trace.
- **New permission added to the catalog after a role already exists** — a subsequent bootstrap run against the same role grants exactly the newly-added codes (Acceptance Criterion D "add/remove relevant setup state"), because `catalogCodes` is re-read from `PermissionDefinitions` on every run rather than cached or hardcoded.
- **`--role-name` omitted** — `roleCode` is reused as the `Name` (trimmed); if that string collides with the `NormalizedName` of an unrelated existing role, `RoleConflict` is returned rather than corrupting either row — an operator supplies a distinct `--role-name` to resolve it, which is expected CLI usage, not a defect.
- **Ordinary staff user without the bootstrapped role** — unaffected by this change; `PermissionAuthorizationHandler` (`PermissionAuthorization.cs:10-36`) still denies any permission the user's roles do not grant (Acceptance Criterion F) — no code in this story touches that handler.

---

## Test Plan

1. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PermissionManagementTests.cs` — extend `Bootstrap_IsIdempotent_AndRejectsInvalidOrInactiveSubjectsWithoutWrites` per Task 4, plus add a new fact `Bootstrap_CreatesMissingRole_GrantsFullCatalog_AndIsIdempotent`: call `BootstrapAsync("agent@example.test", "FIRST_ADMIN", "First Administrator", CancellationToken.None)` against a fresh `RoleManagementDbContext` with no pre-existing role of that code; assert the result succeeds, exactly one `Role` row with that code exists and `IsActive`, `RolePermissions` for that role equals `PermissionDefinitions.CountAsync()` in code set, and one `RoleAuditEvent` with `EventType == "created"` exists. Call `BootstrapAsync` a second time with identical arguments; assert no additional `Role`, `RolePermission`, or `RoleAuditEvent` rows were added (same counts).
2. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PermissionManagementTests.cs` — add `Bootstrap_ExistingInactiveRole_FailsWithoutWrites`: create a role via the local `CreateRoleAsync` helper, deactivate it directly on the context, call `BootstrapAsync` with that role's code; assert `RoleInactive` and that no `RolePermission`/`StaffSubjectRole` rows exist for it.
3. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/AuditTests.cs` — update the two existing bootstrap-related facts per Task 4 (no behavioral change, only the added `roleName: null` argument).
4. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/StaffBootstrapTests.cs` — unchanged; confirms this story does not touch the StaffIdentity bootstrap path.
5. Manual/regression verification (Acceptance Criterion E/F, run against a running API and the endpoints already mapped by each module — `RoleManagementModule.cs:72`, `DepartmentManagementModule.cs:42`, `BranchManagementModule.cs:42`, `SystemConfigurationModule.cs:38`, `CustomerManagementModule.cs:40`): after running the bootstrap tool for a fresh subject/role, sign in as that subject and confirm `GET /api/v1/roles`, `GET /api/v1/departments`, `GET /api/v1/branches`, `GET /api/v1/system-configuration`, and `POST /api/v1/customers` all return non-403; confirm an ordinary authenticated staff subject with no role assignment still receives 403 on each. Record this as a manual verification step in the PR description — no new automated end-to-end test is required by the intake beyond the existing `SquadCrm.Api.Tests` authorization-boundary pattern already covering permission-gated endpoints per module.

---

## Migration / Rollback

No schema migration in this story — `PermissionDefinition`, `Role`, `RolePermission`, `StaffSubjectRole` all already exist (Stories 14/17). Rollback is a plain code revert of `AuthorizationBootstrapService.cs`, `Permissions.cs`, and `BootstrapProgram.cs`; no data cleanup is required beyond whatever role/grants an operator explicitly created by running the tool (the tool remains explicit-invocation-only either way).

---

## Verification Steps

1. **Backend builds:** `dotnet build src/backend/SquadCrm.sln` — no errors.
2. **Backend tests pass:** `dotnet test src/backend/SquadCrm.sln` — all new and existing tests green (requires PostgreSQL reachable per `PostgresTestDatabase`, `docker compose up -d`).
3. **Format check:** `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` — no formatting diffs.
4. **Frontend format check (no frontend code touched, but required by acceptance criteria):** `npm run format:check --prefix src/frontend`.
5. **Manual fresh-environment run:** from a freshly migrated database (`scripts/migrate`, no seed), bootstrap a StaffIdentity account, then run:
   ```bash
   dotnet run \
     --project src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap \
     -- --subject-email agent@example.test --role-code ADMINISTRATOR --role-name Administrator
   ```
   Confirm exit code `0`, `"Authorization bootstrap completed."` on stdout, and re-running the identical command also exits `0` with no duplicate rows (inspect `role_management.role`, `role_management.role_permission`, `role_management.staff_subject_role`).
6. **Regression:** confirm `GET /api/v1/roles/{id}/permissions` for the bootstrapped role (Story 17's endpoint) lists every code present in `GET /api/v1/permissions`, and that the CRM-113/CRM-112 UI/API paths for roles remain unchanged.

---

## Done Criteria

- [x] `AuthorizationBootstrapService.BootstrapAsync` creates a missing named role (active), reuses an existing active one, and fails clearly (no partial writes) on a missing/inactive subject or an inactive existing role.
- [x] The role receives every code currently in `role_management.permission_definition`, derived by query — no hardcoded permission list remains in the codebase (`Permissions.Bootstrap` removed).
- [x] Re-running the tool with identical arguments is a no-op beyond the first run: no duplicate `Role`, `RolePermission`, `StaffSubjectRole`, `RoleAuditEvent`, or `PermissionChangeAuditEvent` rows.
- [x] No HTTP endpoint, startup hook, or hardcoded credential was added; the tool remains reachable only via explicit `dotnet run --project src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap`.
- [x] An ordinary authenticated staff subject without the bootstrapped role remains denied on permission-gated endpoints (no change to `PermissionAuthorizationHandler`).
- [x] `README.md`'s "Bootstrap the first role administrator" section documents the corrected fresh-environment sequence (migrate → StaffIdentity bootstrap → this tool → run the app) without the CRM-110/CRM-112 role-creation prerequisite.
- [x] `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` and `npm run format:check --prefix src/frontend` both pass.
- [x] `00-overview.md` updated with this story.
- [x] The `SquadCrm.RoleManagement.Bootstrap` CLI (Task 2a) actually runs end-to-end against a fresh migrated database via `dotnet run --project src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap`, using the repository's normal `AddSquadCrmPostgres`/`HostApplicationBuilder` wiring — not just the integration-test suite calling `AuthorizationBootstrapService` directly.

## Real end-to-end verification evidence (Task 2a fix)

Performed against a freshly migrated local `docker compose` PostgreSQL (all module contexts migrated: ArchitectureFixture, StaffIdentity, RoleManagement, Audit, DepartmentManagement, SystemConfiguration, BrandingManagement, BranchManagement, CustomerManagement):

1. `SquadCrm.Modules.StaffIdentity.Bootstrap -- agent@example.test` created the staff account.
2. `SquadCrm.RoleManagement.Bootstrap --subject-email agent@example.test --role-code ADMINISTRATOR --role-name Administrator` (built and invoked exactly as an operator would via `dotnet run`) printed `Authorization bootstrap completed.` on the first run — this previously failed with `PostgreSQL configuration is invalid or incomplete.` before the Task 2a fix.
3. Verified in Postgres: `role_management.role` has exactly one active `ADMINISTRATOR` row; `role_management.role_permission` grants all 14/14 rows in `role_management.permission_definition`; exactly one `role_management.staff_subject_role` row; exactly one `role_management.role_audit_event` row with `event_type = 'created'`.
4. Re-ran the identical command: still exactly one role/grant-set/assignment/created-event — confirmed idempotent, no duplicates.
5. Started `SquadCrm.Api`, logged in as `agent@example.test` via `POST /api/v1/auth/login`, and with the returned token: `GET /api/v1/roles` → 200, `GET /api/v1/departments` → 200, `GET /api/v1/branches` → 200, `GET /api/v1/system-configuration` → 200, `POST /api/v1/customers` → 201.
6. Bootstrapped a second, ordinary staff account (`ordinary@example.test`) with no role assignment, logged in, and confirmed all five of the same calls returned 403.

`dotnet test tests/SquadCrm.Persistence.IntegrationTests` (109/109), `dotnet test tests/SquadCrm.ArchitectureTests` (19/19), `dotnet format --verify-no-changes`, and `npm run format:check` were all re-run clean after the Task 2a fix.

**STOP HERE. Report to the user and wait for confirmation before implementation begins.**
