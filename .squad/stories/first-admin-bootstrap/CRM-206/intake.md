# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/first-admin-bootstrap/CRM-206/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Support First Administrator Bootstrap on Fresh Environment
- **Feature slug (folder under `plans/`):** `first-admin-bootstrap`

## Tracker (metadata only)

- **Tracker type:** `linear`
- **Work item id:** `CRM-206` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Bug`
- **Status:** `Backlog`
- **Assignee:** `Mahmoud Said`
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Support First Administrator Bootstrap on Fresh Environment
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
## Problem

A fresh database has no role rows. The existing RoleManagement bootstrap requires an existing active role, while:
- creating a role requires roles.manage;
- granting permissions requires roles.manage;
- no subject can initially have roles.manage.

This is a first-admin bootstrap deadlock: there is no way to provision the first administrator on a genuinely fresh environment through the current RoleManagement tooling.

## Desired Behavior

Extend the existing RoleManagement operator/bootstrap tooling (src/Tools/SquadCrm.RoleManagement.Bootstrap) so an explicitly invoked operator action can provision the first administrator on a fresh database, without seeding a default administrator through EF migrations, without auto-provisioning on startup, without hardcoded credentials, and without bypassing the existing RoleManagement authorization model.

The operator workflow should:
1. Validate that the target StaffIdentity subject already exists.
2. Create a named administrator role if it does not exist.
3. Reuse the role if it already exists.
4. Ensure the role is active where safe/appropriate.
5. Grant the role the complete CURRENT registered permission catalog (not a hardcoded list).
6. Assign the target staff subject to that role using the existing authorization mapping.
7. Be safely idempotent — no duplicate role/permission/assignment rows on repeat runs.
8. Never log passwords, tokens, connection secrets, or other sensitive values.
9. Work against the normal production-capable persistence/configuration path when explicitly invoked.
10. Never run automatically (no startup auto-provisioning, no unauthenticated setup endpoint).
11. Fail clearly when the target staff subject does not exist.
12. Preserve module boundaries — no direct cross-module DbContext access.
13. Reuse existing RoleManagement services/contracts where appropriate.
14. Preserve normal role/permission APIs and UI behavior.

## Permission Catalog

Do not hardcode the old fixed four-permission bootstrap list. Derive/grant the currently registered RoleManagement permission catalog via the existing canonical permission registration/catalog mechanism. If the current architecture makes this impossible without changing a shared contract, treat that as a Decision Gate and report it before implementing an alternative.

## Security Constraints

- Explicit privileged operator action only.
- No HTTP endpoint such as /bootstrap-admin, /setup-admin, /create-first-admin.
- No unauthenticated setup endpoint.
- No startup auto-provisioning.
- No hardcoded admin email or password, no default ADMIN user.
- The staff account must already exist via the existing StaffIdentity bootstrap.

## Documentation

Document the fresh-environment sequence using the repository's actual commands:
1. Apply migrations.
2. Explicitly bootstrap a StaffIdentity account.
3. Explicitly bootstrap the first administrator role/permissions/assignment.
4. Start/use Agent CRM.

Do not invent exact CLI syntax before inspecting the existing bootstrap command structure.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Verify (minimum) against a genuinely fresh migrated database:

A. Missing staff subject → bootstrap fails clearly; no partial admin role/assignment state if appropriate.
B. Existing staff + no roles → bootstrap succeeds; administrator role created; current registered permissions granted; subject assigned.
C. Run bootstrap again → succeeds idempotently; no duplicate role; no duplicate grants; no duplicate assignment.
D. Add/remove relevant setup state where practical and verify reconciliation behavior is deterministic.
E. Login as the bootstrapped staff account and verify representative protected Agent CRM capabilities are accessible, including at least: role management; department management; branch management; system configuration; customer creation; any other currently registered Agent CRM permissions.
F. Verify an ordinary authenticated staff user without these grants remains denied.
G. Run relevant authorization/architecture/integration tests, plus:
   - npm run format:check --prefix src/frontend
   - dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes

Do not expand into unrelated full regression unless required.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| *(e.g. `attachments/flow.png`)* | *(e.g. UX flow)* |

None.

---

## Dependencies

- **Blocked by / related ids:** None.
- **Depends on code areas or other stories:** `src/Tools/SquadCrm.RoleManagement.Bootstrap`; existing StaffIdentity bootstrap tooling; RoleManagement module's permission catalog/registration mechanism.

## Extra notes (optional)

- Prefer extending the existing `src/Tools/SquadCrm.RoleManagement.Bootstrap` rather than creating a competing bootstrap system.
- If deriving the full permission catalog is impossible without changing a shared contract, treat as a Decision Gate and report before implementing an alternative.

## Technical hints (optional)

- Repos/roots: `.`. Backend: ASP.NET Core modular monolith, C#, EF Core/PostgreSQL. Relevant area: RoleManagement module + `src/Tools/SquadCrm.RoleManagement.Bootstrap` operator tool. Frontend not in scope for this story.

## Out of scope

- Login behavior changes.
- StaffIdentity password bootstrap semantics changes.
- New role-management UI.
- Customer features.
- Unrelated modules.
- Authorization redesign.
- Generic CLI infrastructure additions.
- Seeding a permanent default administrator through EF migrations.
- Any HTTP setup/bootstrap-admin endpoint, authenticated or not.
