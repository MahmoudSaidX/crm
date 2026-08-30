# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/manage-roles/CRM-112/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Manage Roles
- **Feature slug (folder under `plans/`):** `manage-roles`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-112` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** (none)
- **Milestone:** Sprint 1 — Security, Administration & Platform Foundation
- **Priority:** High
- **Parent epic:** CRM-108 [Epic] Security & Administration

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Manage Roles
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
## User Story

As an administrator, I want to manage global roles so that reusable responsibility sets can be assigned to CRM staff.

## Business Rules

* Roles are global and do not themselves encode Branch/Department scope.
* Permissions are assigned to roles through the permission capability; user data access additionally evaluates organizational scope.
* System-protected roles, if seeded, cannot be deleted/renamed in ways that break bootstrap administration.
* Prefer deactivate over destructive deletion when a role has references.

## Fields Dictionary

| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| Name | string | Yes | Human-readable; unique normalized name |
| Code | string | Yes | Stable machine identifier; unique; avoid changing after use |
| Description | string | No | Administrative explanation |
| IsActive | boolean | Yes | Defaults true |
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
* Authorized admin can create, view, edit, list, activate and deactivate roles.
* Roles can be assigned to multiple users.
* Role name/code uniqueness is validated.
* Deactivating a role prevents new assignment while preserving historical/user references until reassigned.
* Role changes are audited.
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

- **Blocked by / related ids:** CRM-110 (User Authentication & Session Management) — Done.
- **Blocks:** CRM-111 (Manage Users), CRM-113 (Configure Role Permissions) — both currently Backlog; do not implement permission-assignment or user-role-assignment UI here, only the Role entity itself.
- **Depends on code areas or other stories:** StaffIdentity module/schema established by CRM-110 (staff auth/session). No other module implemented yet in this dependency chain.

## Extra notes (optional)

- CRM-113 (Configure Role Permissions) owns assigning permissions to a role; this story owns only the Role entity CRUD lifecycle.
- CRM-111 (Manage Users) owns assigning roles to users; do not build user↔role assignment UI here — only preserve/expose enough on Role to make future assignment possible (i.e. do not block it architecturally).
- "Audited" (AC) should use whatever audit/domain-event pattern the repository already established (see CLAUDE.md: domain/integration events); do not invent a bespoke audit log.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: C# (ASP.NET Core backend, modular monolith, schema-per-module) and TypeScript/Angular (frontend, PrimeNG).
- Follow existing module conventions from the StaffIdentity module (CRM-110) for entity/module layout, since this is the next module under the same Security & Administration epic (CRM-108).

## Out of scope

- Permission assignment to roles (CRM-113).
- Assigning roles to users (CRM-111).
- Branch/Department scoping — Roles are explicitly global per Business Rules.
- Any organizational-scope enforcement (handled elsewhere per Business Rules).
