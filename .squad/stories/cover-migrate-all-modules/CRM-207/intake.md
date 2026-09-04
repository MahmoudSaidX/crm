# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/cover-migrate-all-modules/CRM-207/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Cover all application module migrations in ./scripts/migrate
- **Feature slug (folder under `plans/`):** `cover-migrate-all-modules`

## Tracker (metadata only)

- **Tracker type:** `linear`
- **Work item id:** `CRM-207` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Improvement`
- **Status:** `Backlog`
- **Assignee:** ``
- **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Cover all application module migrations in ./scripts/migrate
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
Problem: `./scripts/migrate` only applies migrations for the ArchitectureFixture and
StaffIdentity modules. Every other module that owns EF Core migrations (Audit,
BranchManagement, BrandingManagement, CustomerManagement, DepartmentManagement,
RoleManagement, SystemConfiguration) must currently be migrated by hand with separate
`dotnet ef database update` commands. This forces developers to know and maintain that
list themselves, which is easy to get wrong or let go stale as modules are added.

Goal: `./scripts/migrate`, run from the repository root, must apply ALL current
application module migrations required for a usable local Squad CRM database, without
a developer needing to run any commands beyond the script itself.

Out of scope: any new product feature; automatic staff/admin account creation (handled
separately by the StaffIdentity bootstrap and the CRM-206 first-admin bootstrap).

Developer-experience-only change to the local migration workflow, done ahead of the
next feature story.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
- Running `./scripts/migrate` against a fresh Compose PostgreSQL database applies every
  application module's migrations (Audit, BranchManagement, BrandingManagement,
  CustomerManagement, DepartmentManagement, RoleManagement, StaffIdentity,
  SystemConfiguration).
- The script discovers module migration targets from the existing repository structure
  rather than relying on a manually duplicated, easily-stale list — without building a
  generic migration framework.
- ArchitectureFixture may keep its current behavior if still needed, but is treated as
  a test/fixture migration, distinct from application modules.
- Rerunning `./scripts/migrate` against an already-migrated database succeeds
  idempotently (EF reports "no migrations to apply" rather than erroring or duplicating
  state).
- The script fails fast and returns a non-zero exit code if any module's migration
  fails.
- Existing env/config conventions (`env/backend.env`, `POSTGRES_*` vars) are preserved.
- The script does not reset/drop the database, does not create staff/admin accounts,
  and does not modify application business logic.
- No new EF Core migrations are added as part of this story.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| *(e.g. `attachments/flow.png`)* | *(e.g. UX flow)* |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** (tracker ids only; optional short note)
- **Depends on code areas or other stories:**

## Extra notes (optional)

- Anything not captured above (e.g. chat context) — keep short.

## Technical hints (optional)

- Repo root: `.`. Backend at `src/backend`, ASP.NET Core / EF Core / PostgreSQL, schema-per-module.
- Existing script: `scripts/migrate` (bash), sources `env/backend.env`, exports
  `POSTGRES_HOST/PORT/DB/USER/PASSWORD`, `cd`s into `src/backend`, runs
  `dotnet tool restore`, then one `dotnet ef database update --project ... --startup-project
  ... --context ...DbContext` per module. Currently only lists ArchitectureFixture and
  StaffIdentity.
- Every module lives at `src/backend/src/Modules/<Module>/SquadCrm.Modules.<Module>/` and,
  if it owns migrations, has a `Persistence/Migrations/` folder and a
  `Persistence/<Module>DbContext.cs` file (context class `<Module>DbContext`). This naming
  is consistent across all 9 modules found — confirmed by inspecting each `*DbContext.cs`.
- Modules currently found with a `Persistence/Migrations` directory (i.e. real EF
  migrations already exist): ArchitectureFixture, Audit, BranchManagement,
  BrandingManagement, CustomerManagement, DepartmentManagement, RoleManagement,
  StaffIdentity, SystemConfiguration. (BrandingManagement and SystemConfiguration have no
  `.Contracts` project but do have migrations.)
- ArchitectureFixture is a test/fixture module, not an application module — the plan
  should keep it distinguished from the application module list even if the loop that
  applies migrations is unified.
- Because the `<Module>/SquadCrm.Modules.<Module>/Persistence/Migrations` folder
  presence + the `<Module>DbContext` naming convention are structurally reliable, the
  script should discover modules by scanning `src/Modules/*/SquadCrm.Modules.*/Persistence/Migrations`
  directories rather than hardcoding a module list — this avoids a stale manual list
  without building a generic/config-driven migration framework.
- `dotnet ef database update` is safe to rerun (EF tracks applied migrations in the
  `__EFMigrationsHistory` table per context/schema), so idempotency comes for free as
  long as each module's `dotnet ef database update` call is preserved as-is.
- README documents the current local setup; check whether it lists manual per-module
  `dotnet ef database update` commands that would now be replaced by `./scripts/migrate`.

## Out of scope

- Any new product feature.
- Automatic staff/admin account creation (StaffIdentity bootstrap / CRM-206 first-admin
  bootstrap stay separate, manual, post-migrate steps).
- Adding new EF Core migrations.
- Building a generic/config-driven migration runner beyond what's needed here.
- Modifying application business logic.
- Resetting or dropping the database automatically.
