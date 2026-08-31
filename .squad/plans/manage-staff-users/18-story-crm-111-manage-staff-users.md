# Story 18 — Manage Users (Story: CRM-111)

## Deadline Acceptance Override (authoritative)

See `../../stories/manage-staff-users/CRM-111/intake.md` for the full text.
Smallest complete end-to-end slice; no new abstractions; no CRM-118/CRM-119
architecture; no bulk operations; server-side authorization stays
authoritative.

## Prerequisites

- CRM-110 (StaffIdentity/auth), CRM-112 (RoleManagement/`Role`), CRM-113
  (permission catalog, `StaffSubjectRole`, `PermissionAuthorizationHandler`,
  fail-closed policies) are all complete and reused verbatim.

## Story Goal

1. Staff CRUD: create (email + initial password + optional display name /
   department / branch), view, edit (display name / department / branch),
   list with search (email or display name substring), activate/deactivate
   (soft, no delete).
2. Role assignment: view and replace the set of roles held by a staff
   subject, reusing the existing `StaffSubjectRole` table and `roles.view`
   / `roles.manage` policies (no new permission codes for this part).
3. Two new permission codes gate the staff-CRUD surface itself:
   `users.view` (read) / `users.manage` (write), added to the existing
   catalog/bootstrap the same way `roles.view`/`roles.manage` were in
   CRM-113.
4. Frontend: staff list/search/form/role-assignment screens mirroring the
   `roles/*` components, permission-gated the same way, plus a shell nav
   entry.

## Backend Tasks

### 1 — StaffUser profile fields (StaffIdentity)

Add nullable `DisplayName`, `Department`, `Branch` (`HasMaxLength(200)` each)
to `StaffUser` / `StaffIdentityDbContext`. One new migration
(`AddStaffUserProfileFields`). Nullable — no backfill needed for existing
rows.

### 2 — Staff CRUD service + endpoints (StaffIdentity)

`StaffUserContracts.cs`: `CreateStaffUserRequest(Email, Password, DisplayName?, Department?, Branch?)`,
`UpdateStaffUserRequest(DisplayName?, Department?, Branch?)`,
`StaffUserResponse(Id, Email, DisplayName, Department, Branch, IsActive, CreatedAtUtc)`.
`Email` in the response is the stored normalized email (no separate raw-case
column — smallest option; documented deviation below).

`StaffUserService` (internal, mirrors `RoleService`): `CreateAsync` (normalize
+ uniqueness pre-check + `IPasswordHasher<StaffUser>`, duplicate sentinel),
`UpdateAsync`, `GetAsync`, `ListAsync(PaginationRequest, string? search)`
(case-insensitive contains on normalized email / display name),
`ActivateAsync`/`DeactivateAsync`. Every mutation appends one
`AuthenticationEvent` row (`EventType`: `user_created` / `user_updated` /
`user_activated` / `user_deactivated`, `Outcome`: `"succeeded"`) in the same
`SaveChangesAsync` — reuses the existing audit sink, no new table.

Endpoints under `/api/v1/staff-users`, gated `permission:users.view` (reads) /
`permission:users.manage` (writes) — same policy-string convention CRM-113
established; no new project reference needed since ASP.NET Core resolves
policies by name.

### 3 — New permission codes (RoleManagement)

Add `Permissions.UsersView` / `UsersManage`; add to `Permissions.Bootstrap` so
the bootstrapped administrator gets user management too. Add
`PermissionPolicies.UsersView` / `UsersManage` (`"permission:users.view"` /
`"permission:users.manage"`) and their `AddPolicy` registrations in
`RoleManagementModule.RegisterServices`, reusing the existing
`PermissionAuthorizationHandler`/`PermissionRequirement` — no new handler
type. Seed two `PermissionDefinition` rows (`Module = "Staff Management"`).

### 4 — Staff role assignment endpoints (RoleManagement)

`IStaffSubjectReferenceReader` gains `FindByIdAsync(Guid, CancellationToken)`
(StaffIdentity.Contracts + `StaffSubjectReferenceReader` impl) so the
assignment endpoint can 404 on an unknown subject without a new contract.

`StaffRoleAssignmentService`: `GetAssignedRolesAsync(staffSubjectId)` →
`IReadOnlyList<RoleSummary>`, `ReplaceAsync(staffSubjectId, IReadOnlyList<Guid> roleIds)`
— validates the subject exists (404 if not) and every role id exists (422 if
not), replaces `StaffSubjectRole` rows transactionally, appends one new
`StaffRoleAssignmentAuditEvent` row (`Id, StaffSubjectId, EventType, RoleCodes, ChangedByHandle, OccurredAtUtc`
— same shape family as `PermissionChangeAuditEvent`, new table because the
existing one is keyed to a single `RoleId` and doesn't fit a
subject-to-many-roles change). One migration adds this table alongside the
Task 3 catalog seed.

Endpoints: `GET/PUT /api/v1/staff-users/{staffSubjectId:guid}/roles`, gated
`roles.view` / `roles.manage` (existing policies — assigning roles is a
role-management action, not a new permission axis).

## Frontend Tasks

### 5 — Staff API service + screens

`staff-users.service.ts` mirrors `roles.service.ts` (list/get/create/update/
activate/deactivate + `getRoles`/`replaceRoles`). `staff-user-list.ts/html`
mirrors `role-list.*` plus a search input. `staff-user-form.ts/html` mirrors
`role-form.*` (password field shown only on create). `staff-user-roles.ts/html`
mirrors `role-permissions.*`, toggling checkboxes over `RolesService.list(1, 200)`
(requires the signed-in admin also hold `roles.view` — an accepted existing
coupling, not new).

### 6 — Routes + nav + i18n

Routes under `/staff-users*`, guarded by `requirePermission('users.view'/'users.manage')`,
mirroring the `roles/*` route block. Shell nav entry gated on
`authorization.state.has('users.view')`. New `staff-user-translations.ts`
(English + Arabic), registered alongside `ROLE_TRANSLATIONS`.

## Edge Cases

- Duplicate email (case/whitespace-insensitive) rejected pre-insert and by
  the existing unique index.
- Unknown id on any staff or role-assignment endpoint → 404, never 500.
- Unknown/duplicate role id in a replace request → 422, no partial write.
- Deactivating a staff user never deletes it or its role assignments;
  existing session/JWT validation already fails a deactivated user's
  requests (`ValidateActiveSessionAsync`) — this story adds no new check
  there.
- Empty `Department`/`Branch`/`DisplayName` accepted as `null`.

## Test Plan

1. Persistence integration: staff CRUD (create/update/activate/deactivate +
   audit event), duplicate-email rejection, search filter, role-assignment
   replace + audit row + unknown-subject/unknown-role rejection.
2. API: new `/api/v1/staff-users*` and `/api/v1/staff-users/{id}/roles*`
   routes reject anonymous (401).
3. Architecture tests: unmodified rules still pass with the extended
   modules.
4. Frontend: `staff-users.service.spec.ts` (mirrors `roles.service.spec.ts`).

## Verification Steps

1. `dotnet build SquadCrm.sln --no-restore` — no errors.
2. Targeted new/changed backend tests (persistence integration + API
   boundary) — green.
3. `ng test agent-crm` — green.
4. `ng build agent-crm --configuration=production` — succeeds (budget
   warning noted, not fixed).

## Done Criteria

- [ ] Authorized staff can create/view/edit/list/search/activate/deactivate
      staff users via `/api/v1/staff-users*` and `agent-crm` `/staff-users`
      screens.
- [ ] Authorized staff can view/replace a staff subject's role assignments.
- [ ] `users.view`/`users.manage` gate the staff-CRUD surface; `roles.view`/
      `roles.manage` gate the assignment surface. All fail closed.
- [ ] Department/Branch are plain optional fields; no CRM-118/119 scope
      added.
- [ ] No bulk operations, no new identity abstraction, no auth/role/permission
      redesign.

**STOP HERE only for a genuine blocker or cross-story architectural
decision — otherwise proceed straight to implementation per the user's
standing instruction for this story.**
