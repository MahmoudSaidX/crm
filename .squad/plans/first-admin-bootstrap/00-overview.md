# first-admin-bootstrap — plan overview

Entry point for the **first-admin-bootstrap** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 22 | [`22-story-crm-206-first-admin-bootstrap.md`](22-story-crm-206-first-admin-bootstrap.md) | Support First Administrator Bootstrap on Fresh Environment | CRM-206 | Story 14 (`../manage-roles/14-story-manage-roles.md`), Story 17 (`../configure-role-permissions/17-story-crm-113.md`), StaffIdentity bootstrap tool |

## Dependency notes

Story 22 extends `src/Tools/SquadCrm.RoleManagement.Bootstrap` and `AuthorizationBootstrapService`, both created by earlier RoleManagement stories (14, 17). It reads the `role_management.permission_definition` catalog those stories populate; it does not add a new schema or table.
