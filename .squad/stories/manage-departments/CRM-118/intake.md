# CRM-118 — Manage Departments

## Story
As an administrator, I want to manage departments so that CRM work, staff
memberships, routing and reporting can use a consistent organizational
structure.

## Acceptance Criteria
- Authorized admin can create, edit, list, view, activate and deactivate departments.
- Arabic and English display names are supported.
- Departments can participate in user organizational memberships.
- Inactive departments cannot be selected for new memberships/routing/configuration but historical references remain readable.
- Department changes are audited.

## Business Rules
- Department is organization structure, not a security role.
- A user's access to department data requires appropriate permission plus an allowed membership/scope unless organization-wide access is explicitly granted.
- Deactivation is preferred to deletion when referenced.
- Department code is stable for integration/configuration use.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| Code | string | Yes | Unique stable identifier |
| ArabicName | string | Yes | Localized display name |
| EnglishName | string | Yes | Localized display name |
| Description | string | No | Administrative description |
| IsActive | boolean | Yes | Defaults true |

## Scope note
"Historical references remain readable" and "participate in user
organizational memberships" describe how *other* modules (CRM-111 Manage
Users, ticket/customer scoping) will reference Department later — this story
only builds the Department catalog itself (CRUD + activate/deactivate),
mirroring CRM-119 (Branches, same shape) and reusing the CRM-112 (Roles)
implementation pattern. No membership UI is built here.
