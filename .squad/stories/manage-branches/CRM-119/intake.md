# CRM-119 — Manage Branches

## Story
As an administrator, I want to manage branches so that CRM data and staff
access can be organized across multiple physical/operational branches.

## Acceptance Criteria
- Authorized admin can create, edit, list, view, activate and deactivate branches.
- Arabic and English display names are supported.
- Branches can participate in user organizational memberships and later customer/ticket/report scope.
- Inactive branches cannot be selected for new memberships/business records but historical references remain readable.
- Branch changes are audited.

## Business Rules
- Branch is organization structure, not a security role.
- Permission alone does not grant access to every branch; organizational scope is evaluated separately.
- Deactivation is preferred to deletion when referenced.
- Branch code is stable for integrations/configuration.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| Code | string | Yes | Unique stable identifier |
| ArabicName | string | Yes | Localized display name |
| EnglishName | string | Yes | Localized display name |
| Description | string | No | Administrative description |
| IsActive | boolean | Yes | Defaults true |

## Scope note
This story mirrors CRM-118 (Departments), same shape — CRUD + activate/
deactivate only. Downstream membership/scope UI in other modules is out of
scope here.
