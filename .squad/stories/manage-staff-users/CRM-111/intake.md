# Intake — CRM-111 Manage Users

## Deadline Acceptance Override (authoritative for this story)

> Implement straightforward staff-user CRUD/search, activate/deactivate, role
> assignment, and basic department/branch memberships using the existing
> auth/role foundations. Keep server-side authorization. Advanced membership
> policy, cross-scope administration rules, bulk operations, and new identity
> abstractions are stretch/non-blocking.

This override supersedes older production-grade acceptance criteria where they
conflict. Priority is a demonstrable, working vertical slice by the Wednesday
demo, not organizational-architecture completeness.

## Explicitly out of scope for this story

- CRM-118 / CRM-119 (Branch/Department domain architecture, scope-enforcement,
  hierarchies) — this story only adds two plain free-text fields
  (`Department`, `Branch`) on the staff record. No lookup tables, no scope
  enforcement, no cross-story architecture.
- Bulk operations of any kind.
- Password reset / forgot-password flows (admin sets an initial password at
  creation only).
- New identity abstractions — reuses `StaffUser`, `ICurrentUserAccessor`,
  `IStaffSubjectReferenceReader`, and the existing `RoleManagement` permission
  model verbatim.
- Any redesign of authentication, roles, or permissions (CRM-110/112/113
  remain unchanged in shape).

## Reused foundations

- `SquadCrm.Modules.StaffIdentity` — owns `StaffUser`; this story extends it
  with profile fields and CRUD/search endpoints, not a new module.
- `SquadCrm.Modules.RoleManagement` — already owns `StaffSubjectRole` (staff
  subject → role assignment) and the permission-catalog/policy machinery
  (`Permissions`, `PermissionPolicies`, `PermissionAuthorizationHandler`) from
  CRM-113; this story adds two new permission codes (`users.view`,
  `users.manage`) and the assignment-replace endpoints on top of the existing
  generic requirement/handler — no new handler.
- `IStaffSubjectReferenceReader` (StaffIdentity.Contracts) gains one new
  method, `FindByIdAsync`, so RoleManagement's assignment endpoint can
  validate a staff subject exists without a new contract or project
  reference.
