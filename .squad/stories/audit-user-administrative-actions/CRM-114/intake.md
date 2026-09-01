# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/audit-user-administrative-actions/CRM-114/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Audit User and Administrative Actions
- **Feature slug (folder under `plans/`):** `audit-user-administrative-actions`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-114` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** ``
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Audit User and Administrative Actions
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Provide one reusable, append-only audit record capability and a simple authorized audit list/detail view. Record actor, action, entity, timestamp and safe metadata for material operations as stories integrate it.

Explicitly OUT OF SCOPE / stretch (non-blocking for this story): advanced audit analytics, retention/archival engines, export, tamper-evidence infrastructure (hashing/chaining/WORM storage), and exhaustive before/after diff engines.

Source note: Linear MCP was unavailable when this intake was authored. This description is the authoritative Deadline Acceptance Override text supplied directly by the product owner (Mahmoud Said) in place of the Linear issue body. If a Linear issue for CRM-114 exists with additional Business Rules/Fields Dictionary content, it has not been reconciled against this intake — surface any drift found later rather than silently trusting either source.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- [ ] A single reusable audit-record capability exists that any module/story can call to record: actor (staff/user), action, entity type, entity id, timestamp, and optional safe metadata (structured, no secrets).
- [ ] Audit records are append-only through normal application behavior (no update/delete API or UI path).
- [ ] An authorized audit list view exists with minimal useful search/filter (e.g. by entity type, action, actor, date range) — "useful minimal level" only, no analytics.
- [ ] An authorized audit detail view exists showing the full record (actor, action, entity, timestamp, metadata).
- [ ] Viewing audit records is gated by permission(s) added to the existing role/permission system (reuse CRM-113 authorization foundation), following Permission + Organizational Scope + Resource Ownership where applicable.
- [ ] Audit metadata never contains passwords, tokens, OTPs, secrets, or otherwise unsafe payloads.
- [ ] The capability is proven end-to-end by wiring it into a small number of already-existing material administrative operations (do not retrofit every prior story).
- [ ] EN/AR UI text is provided and the existing responsive shell/layout is reused for the new screens.
- [ ] No audit analytics, export, retention engine, tamper-evidence infrastructure, event-sourcing, configurable rule engine, or new cross-module messaging architecture is introduced.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | — |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CRM-113 (role-based permission management — reuse its permission/authorization foundation), CRM-111 (staff user management — likely source of a "material administrative operation" to wire audit into), CRM-105 (ASP.NET Core modular monolith module conventions), CRM-106 (PostgreSQL + EF Core schema-per-module conventions). None of these are known to be blocked/incomplete as of this intake.
- **Depends on code areas or other stories:** Existing authentication/authorization module (CRM-113), existing module/persistence/API/frontend patterns established by prior stories.

## Extra notes (optional)

- Reconciliation evidence (gathered read-only prior to this intake) found audit-event classes already embedded in the RoleManagement module: `RoleAuditEvent`, `StaffRoleAssignmentAuditEvent`, `PermissionChangeAuditEvent`, persisted via `RoleManagementDbContext` (likely landed with CRM-113). CRM-114 asks for "one reusable" append-only audit capability — the plan must explicitly address how these pre-existing, module-local audit records relate to the new shared capability (e.g. keep them as-is inside RoleManagement and add a new, separate, reusable audit module for other stories going forward, vs. some other reconciliation) rather than silently duplicating or silently replacing them. This is flagged for architecture review, not resolved here.
- No existing dedicated "Audit" module was found anywhere else in the backend or frontend.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `typescript`.
- Backend: ASP.NET Core modular monolith, PostgreSQL + EF Core, schema-per-module, no cross-module DbContext access — a new audit capability should live in its own module (or a small shared kernel) with its own schema, exposed to other modules only via an explicit contract (e.g. an `IAuditRecorder`-style port), per CLAUDE.md.
- Frontend: Angular + PrimeNG, existing responsive shell (CRM-117-ish) and EN/AR RTL/LTR localization pattern already established — reuse, don't rebuild.
- Existing precedent for module-local audit persistence: `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/Persistence/{RoleAuditEvent,StaffRoleAssignmentAuditEvent,PermissionChangeAuditEvent}.cs` and `RoleManagementDbContext.cs`.

## Out of scope

- What this story explicitly does **not** cover: audit analytics/dashboards, export, retention/archival policies, tamper-evidence infrastructure (hash chaining, WORM), exhaustive before/after diffing, a configurable audit rule engine, event-sourcing, new cross-module messaging/eventing architecture, retrofitting every previously completed story with audit calls.
