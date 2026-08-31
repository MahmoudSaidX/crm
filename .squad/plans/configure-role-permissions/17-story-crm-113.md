# Story 17 — Configure Role Permissions (Story: CRM-113)

## Prerequisites

- Story 13 completed: `../user-authentication-session-management/13-story-crm-110-user-authentication-session-management.md` — active staff subjects, revocable sessions, `ICurrentUserAccessor`, and JWTs containing only `sub`/`sid`.
- Story 14 completed: `../manage-roles/14-story-manage-roles.md` — global roles, RoleManagement APIs/UI/schema, and module-local transactional audit precedent.
- Story 6 completed: `../shared-api-validation-security-foundation/06-story-shared-api-validation-security-foundation.md` — ASP.NET authorization registration and Problem Details conventions.
- Approved architecture/security decision: RoleManagement owns minimum persistent staff-subject-to-role authorization mapping; StaffIdentity exposes a read-only subject reference contract; grants are resolved server-side on every request; bootstrap is explicit operator tooling only.

---

## Story Goal

Let an administrator view the stable permission catalog, see and replace permissions assigned to a global role, and have current backend role operations enforced by those permissions. Add frontend permission-aware navigation/actions and clean 403 handling. Audit permission changes transactionally and apply them on the next request without permission claims or an authorization cache.

Do not implement CRM-111 user administration, CRM-114 general auditing, Branch/Department membership, resource ownership, departments, branches, or customer portal models.

---

## Context — Read These Files First

1. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/RoleManagementModule.cs` — lines 19–43 register RoleManagement and protect role endpoints with bare `RequireAuthorization()`; replace these with permission policies and add catalog/assignment/current-grant endpoints.
2. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/RoleManagementDbContext.cs` — lines 10–42 own the `role_management` schema and audit mapping; add only permission, role-permission, staff-subject-role, and permission-change-audit tables.
3. `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/RoleService.cs` — mutating methods add `RoleAuditEvent` and call one `SaveChangesAsync`; mirror that transaction boundary.
4. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/Persistence/StaffUser.cs` — lines 3–10 have no role relationship. Do not add one.
5. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/AuthenticationService.cs` — lines 132–155 issue only `sub` and `sid`; do not add permission claims.
6. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/SquadCrm.Modules.ArchitectureFixture.Contracts.csproj` — lines 1–13 are the module contract precedent.
7. `src/backend/tests/SquadCrm.ArchitectureTests/ModuleProjectDependencyRulesTests.cs` — lines 8–50 allow cross-module references only through `*.Contracts`.
8. `src/frontend/projects/agent-crm/src/app/auth/auth.interceptor.ts` — lines 6–20 clear auth only for 401; add clean 403 navigation without logout.
9. `src/frontend/projects/agent-crm/src/app/shell/agent-shell.ts` — lines 19–31 always display Roles navigation; filter it by grants.
10. `src/frontend/projects/agent-crm/src/app/roles/role-list.ts` and `roles.service.ts` — extend the existing localized responsive role feature.
11. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap/BootstrapProgram.cs` — lines 55–95 are the explicit operator-command precedent; add a dedicated narrow RoleManagement bootstrap executable.

---

## Product rules (from story)

- Stable definitions are code-owned catalog entries: `roles.view` and `roles.manage`.
- `roles.view` protects role/catalog/assignment reads. `roles.manage` protects role mutations and permission replacement.
- Backend checks are authoritative; frontend visibility is UX only.
- Current resource models have no Branch/Department or customer-owned data, so add no speculative scope/ownership model.
- A role never grants organizational scope.

---

## Backend Tasks

### 1 — StaffIdentity read-only contract

Create `SquadCrm.Modules.StaffIdentity.Contracts`, dependency-free except BuildingBlocks.Abstractions, with `IStaffSubjectReferenceReader` returning immutable `StaffSubjectReference` (`Id`, `IsActive`) by exact normalized email. Implement with `AsNoTracking` inside StaffIdentity and register it. RoleManagement references only the contracts project.

### 2 — Permission persistence

Extend `RoleManagementDbContext` with `PermissionDefinition`, `RolePermission`, `StaffSubjectRole`, and `PermissionChangeAuditEvent`. Use composite keys for joins, a Role FK, and no cross-schema FK for the already-validated opaque subject id. Seed `roles.view` and `roles.manage` in one EF migration. Seed no user, credential, role assignment, or administrator role.

### 3 — Server-side authorization

Add an authorization requirement/handler in RoleManagement. Parse the authenticated subject handle as Guid, query active roles joined to current grants, and succeed only for the required code. Query every authorization check; never cache or add JWT permission claims. Register policies for `roles.view` and `roles.manage`.

### 4 — APIs and audit

Add `GET /api/v1/authorization/me`, `GET /api/v1/permissions`, `GET /api/v1/roles/{id}/permissions`, and `PUT /api/v1/roles/{id}/permissions`. Validate a distinct set of known nonblank codes. Replace grants and insert one audit row in one transaction. Apply view/manage policies to existing role read/write endpoints. A self-revocation takes effect on the next request.

### 5 — Explicit bootstrap command

Create `SquadCrm.Modules.RoleManagement.Bootstrap`. Require `--subject-email` and `--role-code`; require PostgreSQL configuration. Resolve exactly one active subject via the StaffIdentity contract, resolve exactly one active existing role, then idempotently assign that subject and grant the minimum `roles.view`/`roles.manage` permissions through normal RoleManagement services. Reject invalid/inactive/missing/ambiguous input; accept and log no secrets; never run at API startup. Document prerequisites and privileged use.

### 6 — Migration and wiring

Generate one RoleManagement migration, update its snapshot, add contracts/bootstrap projects to `SquadCrm.sln`, and extend relevant project/architecture references.

---

## Frontend Tasks

### 7 — Grant state, guards, and 403 UX

Create an authorization service loading `/api/v1/authorization/me`, storing permission codes in signals, exposing `has(code)`, and clearing on logout/401. Add permission route guards. Add a 403 interceptor that navigates to a localized forbidden page without clearing authentication.

### 8 — Permission-aware role UI

Hide Roles navigation without `roles.view`. Hide New/Edit/activate/deactivate/configure controls without `roles.manage`. Extend `RolesService` and add `/roles/:id/permissions`, grouped by module, using PrimeNG checkboxes/button and existing localization/responsive shell patterns.

---

## Edge Cases & Failure Modes

- Anonymous returns 401 before permission evaluation.
- Unassigned subject, inactive role, malformed subject id, or missing permission fails closed with 403.
- Unknown/duplicate/blank replacement codes write neither grants nor audit.
- Deactivated role stops authorizing immediately.
- Revocation applies on the next request because grants are queried server-side.
- Invalid bootstrap subject/role exits nonzero without mutation; repeated valid invocation is idempotent.
- Bootstrap never creates a subject, role, credential, or default administrator.
- Database keys prevent duplicate mappings and permission replacement remains transactional.
- Angular 403 shows a localized forbidden page and retains authentication.

---

## Test Plan

1. Persistence integration: catalog seed, add/remove/audit, invalid codes, immediate revocation, inactive-role denial, bootstrap idempotency and invalid-input no-write.
2. API: anonymous 401, authenticated unassigned 403, view/manage allow/deny matrix, current grants, replacement, and JWTs without permissions.
3. Architecture: contract dependency purity and no cross-module DbContext/table access.
4. Frontend: grant service/guard, 403 behavior, hidden navigation/actions, API requests, and permission screen save/error behavior.
5. Browser: English and Arabic/RTL permission UI, responsive shell, denied state, and revoked access.

---

## Migration / Rollback

Apply the RoleManagement migration before the updated API. Roll application code back with the new tables retained unless authorization mappings can be discarded. A half-applied migration must fail authorization closed, never revert to authentication-only access.

---

## Verification Steps

1. **Backend builds:** from `src/backend`, run `dotnet build SquadCrm.sln --no-restore`.
2. **Backend tests:** run targeted API, persistence integration, and architecture test projects.
3. **Frontend runs:** from `src/frontend`, run agent-crm tests and production build.
4. **Regression:** run repository format/lint checks required for touched C# and Angular files.
5. **Browser:** verify allowed, denied, revoked, English, Arabic/RTL, and responsive states with an explicitly bootstrapped test subject/role.

---

## Done Criteria

- [ ] Admin can view the catalog and each role's grants.
- [ ] Admin can add/remove valid permissions from an active global role.
- [ ] Current RoleManagement endpoints enforce `roles.view`/`roles.manage` authoritatively.
- [ ] Missing grants, malformed subject, or inactive role fails closed.
- [ ] No speculative organizational scope/ownership implementation is added.
- [ ] Frontend hides unauthorized navigation/actions and handles 403 without logout.
- [ ] Changes are transactionally audited and effective next request without JWT grants/cache.
- [ ] Operator bootstrap is validated, minimal, idempotent, documented, and creates no default identity/credential.
- [ ] CRM-111, CRM-114, CRM-118, and CRM-119 remain unimplemented.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 18.**
