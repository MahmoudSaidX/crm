# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/configure-role-permissions/CRM-113/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Configure Role Permissions
- **Feature slug (folder under `plans/`):** `configure-role-permissions`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-113` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** `(none)`
- **Milestone:** Sprint 1 — Security, Administration & Platform Foundation
- **Priority:** Urgent
- **Parent epic:** CRM-108 Security & Administration

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Configure Role Permissions
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
## User Story

As an administrator, I want to configure permissions for global roles so that CRM actions are authorized consistently while data access remains constrained by organizational scope.

## Business Rules

* Authorization = Permission + Organizational Scope + Resource Ownership when the resource model requires ownership.
* Frontend checks are never sufficient authorization.
* Permission identifiers are stable machine-readable capabilities such as tickets.view or tickets.assign.
* A role cannot grant organizational scope; scope comes from memberships or explicit organization-wide authorization.
* Customer Portal ownership rules are stronger: a customer can access only their own resources regardless of staff permissions.

## Fields Dictionary

| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| PermissionCode | string | Yes | Stable unique machine capability |
| Name | string | Yes | Administrative display label |
| Module | string | Yes | Owning business capability |
| Description | string | No | Explains allowed action |
| RoleId | UUID | Yes | Active role being configured |
| Granted | boolean | Yes | Whether permission belongs to role |
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
* Admin can view the permission catalog and permissions assigned to each role.
* Admin can add/remove allowed permissions from a role.
* Backend endpoints/application use cases enforce required permissions.
* Data-returning operations additionally enforce Branch/Department scope and resource ownership where applicable.
* Frontend hides/disables unauthorized navigation/actions and handles 403 responses cleanly.
* Permission changes are audited and take effect according to session/authorization cache policy.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
None.

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CRM-110 (Done), CRM-112 (Done).
- **Depends on code areas or other stories:** CRM-204 shared API/security foundation (Done); CRM-110 StaffIdentity authentication/session and current subject; CRM-112 RoleManagement global roles.

## Extra notes (optional)

- Approved architecture/security decision for this story:
  - RoleManagement owns the minimum persistent staff-subject-to-role authorization mapping. This is an authorization primitive only: no user-management workflow, UI, CRUD, memberships, or bulk assignment.
  - StaffIdentity exposes a minimum read-only subject validation/reference contract. RoleManagement must not access the StaffIdentity DbContext or tables.
  - Resolve current grants server-side; never place authoritative permissions in JWTs. Fail closed. Permission changes therefore take effect on the next authorization check without session invalidation or an authorization cache.
  - Provide a production-capable, explicitly invoked operator bootstrap command only. No automatic startup bootstrap, default admin, or default credential. It validates that exactly one target subject exists and is eligible, performs the minimum role assignment through normal persistence/domain paths, is safely idempotent, rejects invalid/ambiguous input, handles no secrets, and is documented as privileged tooling. Do not introduce a generic CLI framework.
  - Permission-change audit is module-local and transactional; CRM-114 remains out of scope.
- The implemented resource model currently has no Branch/Department or customer-owned resources. Do not invent organizational scope or ownership models; enforce those dimensions only when applicable.

## Technical hints (optional)

- Existing RoleManagement owns the role schema/API/UI and module-local audit precedent. Extend it directly.
- Existing StaffIdentity owns authenticated staff identities and validates active sessions on every request.
- Existing backend policies are registered through `AddAuthorization`; current RoleManagement endpoints use bare `RequireAuthorization()` and must become permission-protected.
- Frontend is Angular/PrimeNG with CRM-116 localization and CRM-117 responsive shell. Follow existing role screens, route guards, API services, and 401 handling patterns.
- Repos/roots: `.`. Primary languages: C# and TypeScript.

## Out of scope

- CRM-111 user-management screens/workflows/CRUD, general role assignment, Branch/Department memberships, or bulk assignment.
- CRM-114 general audit capability.
- CRM-118 departments and CRM-119 branches.
- Organizational-scope or resource-ownership infrastructure for resource models that do not yet exist.
- Permission claims in JWTs or a speculative authorization cache.
- Automatic/default administrator creation or credentials.
