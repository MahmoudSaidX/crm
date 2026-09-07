# Story intake

- Folder: `.squad/stories/configure-ticket-categories/CRM-131/intake.md`

---

## Feature

- **Feature name (display):** Configure Ticket Categories
- **Feature slug (folder under `plans/`):** `configure-ticket-categories`

## Tracker (metadata only)

- **Tracker type:** `linear`
- **Work item id:** `CRM-131`
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** (agent)
- **Labels:** `Ticket Management`

---

## Title

```
Configure Ticket Categories
```

---

## Description

```
As an administrator, I want to manage ticket categories so that support requests are classified consistently.

This is the first story of the Ticket Management epic (CRM-130). There is no Ticket entity in
this codebase yet — this story only builds the category management data model and admin CRUD,
not ticket selection UI or ticket routing behavior.

Deadline Acceptance Override note from Linear: implement simple category CRUD with Arabic/English
name, active state, optional default department. Subcategory trees, generalized taxonomy, complex
routing metadata and dependency policies are explicitly stretch/non-blocking — do NOT build them.

Fields Dictionary: Id (UUID), Code (string, required/unique), ArabicName (string), EnglishName
(string), DefaultDepartmentId (nullable UUID), IsActive (boolean), SortOrder (integer).
```

---

## Acceptance criteria

```
- Authorized admin can create, edit, list, activate and deactivate categories.
- Arabic and English names are supported.
- Category may map to a default department for routing.
- Active categories are selectable on tickets; inactive ones remain historically readable
  (this story only implements the category data model/flag supporting this rule — IsActive flag
  and unique/stable Code — since there is no Ticket entity yet to consume it).

Business rules:
- Category code is unique and stable.
- Inactive categories cannot be selected for new/changed tickets going forward (no ticket
  selection UI exists yet to enforce this in this story; the data model must support it).
- Existing tickets keep their historical category reference — not applicable yet, no Ticket
  entity exists; do not invent one.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CRM-130 (Ticket Management epic, parent). CRM-118 (Manage
  Departments — established CRUD + admin-config pattern to mirror). CRM-128/129 (recent
  CustomerManagement module work, for cross-module read pattern and admin UI conventions).
- **Depends on code areas or other stories:**
  - `src/backend/src/Modules/DepartmentManagement/` — CRUD module pattern to mirror exactly
    (entity, service, module registration, endpoints, permission policy naming, migrations).
  - `src/backend/src/Modules/DepartmentManagement/SquadCrm.Modules.DepartmentManagement.Contracts/IDepartmentActiveLookup.cs`
    — existing cross-module contract to reuse for validating `DefaultDepartmentId` (same pattern
    CustomerManagement already uses for its own `DepartmentId`).
  - `src/backend/src/Modules/RoleManagement/` — permission catalog: new module needs its own
    `Permissions.cs`/`PermissionPolicies` local class (existing per-module convention, not shared)
    and a new RoleManagement migration seeding `ticketcategories.view`/`ticketcategories.manage`
    into `permission_definition` (mirrors `AddDepartmentPermissions` migration).
  - `src/frontend/projects/agent-crm/src/app/departments/` — Angular list/form pattern (PrimeNG
    table + reactive form), translations, routes, nav item — to mirror for the new
    `ticket-categories` feature area.
  - `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresTestDatabase.cs` — needs a
    new module DbContext migrate call + factory accessor, following the existing per-module list.

## Extra notes (optional)

- New backend module: `SquadCrm.Modules.TicketManagement` (schema-per-module,
  `ticket_management` schema), first story of the Ticket Management epic (CRM-130). No existing
  module or frontend area for it yet.
- No Ticket entity exists in this codebase. Do not create one. Do not build ticket-side
  selection UI or routing logic — only the category data model/CRUD that a future story will
  consume.
- Reuse the exact established patterns aggressively: this is a standard CRUD admin story with a
  precedent (Departments, CRM-118) already in the repo. No architecture review needed per the
  task brief; escalate only if a genuine conflict with an established pattern is found.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `csharp`,
  `typescript`.
- Backend: ASP.NET Core minimal APIs, EF Core + Npgsql, schema-per-module, permission-policy
  authorization (`RequireAuthorization(PermissionPolicies.X)`), audit recording via
  `IAuditRecorder`, Postgres unique-violation (`23505`) race-safe duplicate handling — all as
  already implemented in `DepartmentManagementModule`/`DepartmentService`.
- Frontend: Angular standalone components, PrimeNG (`p-table`, `p-button`, `p-tag`, `p-select`,
  `p-message`, `InputTextModule`, reactive forms), `LocalizationService`/`TranslationResources`
  for Arabic/English + RTL/LTR, `AuthorizationState`/`requirePermission` guard for
  permission-gated routes/actions — all as already implemented in the `departments/` feature
  folder.
- New permissions: `ticketcategories.view`, `ticketcategories.manage` (module-local
  `Permissions`/`PermissionPolicies` constants + RoleManagement `permission_definition` seed
  migration), following the exact `departments.view`/`departments.manage` precedent.

## Out of scope

- Ticket entity, ticket creation/selection UI, ticket routing/assignment logic (CRM-130's other,
  later stories).
- Subcategory trees, generalized taxonomy, complex routing metadata, dependency policies
  (explicitly stretch/non-blocking per the Linear deadline acceptance override).
- Enforcing "inactive category cannot be selected" at the point of ticket creation — no Ticket
  entity exists yet to enforce this against; only the supporting data (`IsActive`, unique
  `Code`) is built now.
