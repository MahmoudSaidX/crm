# configure-role-permissions — plan overview

Entry point for the **configure-role-permissions** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 17 | `17-story-crm-113.md` | Configure Role Permissions | CRM-113 | CRM-110, CRM-112, CRM-204 |

## Dependency notes

CRM-113 extends CRM-112 RoleManagement and consumes a read-only CRM-110 StaffIdentity contract. It does not implement CRM-111 user management.
