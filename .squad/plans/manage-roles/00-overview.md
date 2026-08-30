# manage-roles — plan overview

Entry point for the **manage-roles** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 14 | [14-story-manage-roles.md](14-story-manage-roles.md) | Manage Roles | CRM-112 | Story 13 (user-authentication-session-management, CRM-110) |

## Dependency notes

- Story 14 adds a new `SquadCrm.Modules.RoleManagement` module, depending only on `SquadCrm.BuildingBlocks` and `SquadCrm.Infrastructure.Postgres` (same shape as `StaffIdentity`). It reuses `ICurrentUserAccessor` (registered by `StaffIdentity`) without a project reference to that module.
- CRM-111 (Manage Users) and CRM-113 (Configure Role Permissions), both Backlog, will depend on the `Role` entity this story introduces; neither is implemented here.
