# Story 23 — Cover all application module migrations in ./scripts/migrate (Story: CRM-207)

## Prerequisites

- None. This story only changes `scripts/migrate`, `README.md`, and `src/backend/README.md` — no application source, no new migrations, no schema change.

---

## Story Goal

`./scripts/migrate`, run from the repository root, must apply **every** current application module's EF Core migrations against the PostgreSQL database started by `docker compose up`, so a developer never runs a manual `dotnet ef database update` for any module by hand.

Today `scripts/migrate` only calls `dotnet ef database update` for `ArchitectureFixture` and `StaffIdentity` (`scripts/migrate:22-30`). The other 7 modules that already own real migrations — `Audit`, `BranchManagement`, `BrandingManagement`, `CustomerManagement`, `DepartmentManagement`, `RoleManagement`, `SystemConfiguration` — are silently skipped, forcing a developer to discover and run each one manually.

The fix must discover the module list from the repository's own structure rather than hardcoding a second list that can drift from the module set already declared in `src/Api/SquadCrm.Api/Program.cs:157-168`. `ArchitectureFixture` is a test/fixture module and stays distinguished from the application module list in the script's own structure (a separate, explicit line), even though the same `dotnet ef database update` loop logic applies to it.

**Not in scope**: any new EF Core migration, any change to an existing `DbContext` or migration file, automatic staff/admin account creation, database reset/drop, a generic/config-driven migration-runner framework, or any application business logic change.

---

## Context — Read These Files First

1. `scripts/migrate` (full file, 30 lines) — the script being extended. Lines 1-20 set up `backend_env_file` sourcing and export the five `POSTGRES_*` vars (defaulted); line 21 `cd`s into `src/backend`; line 22 runs `dotnet tool restore`; lines 23-30 are two hardcoded `dotnet ef database update --project ... --startup-project ... --context ...DbContext` blocks, one per module (`ArchitectureFixture`, `StaffIdentity`). This is the only place the module list is (incompletely) enumerated today.
2. `src/backend/src/Api/SquadCrm.Api/Program.cs:155-168` — the authoritative, compile-time `IModule[] modules` list (`AuditModule`, `StaffIdentityModule`, `RoleManagementModule`, `DepartmentManagementModule`, `BranchManagementModule`, `CustomerManagementModule`, `SystemConfigurationModule`, `BrandingManagementModule`, `ArchitectureFixtureModule`) with the comment "Explicit module list… No runtime assembly scanning." This is the set of modules the running application actually wires up; the migrate script's coverage must match it (8 application modules + `ArchitectureFixture`).
3. Confirmed by directory listing: every module directory matching `src/backend/src/Modules/*/SquadCrm.Modules.<Module>/Persistence/Migrations` exists for exactly these 9 module project directories: `ArchitectureFixture`, `Audit`, `BranchManagement`, `BrandingManagement`, `CustomerManagement`, `DepartmentManagement`, `RoleManagement`, `StaffIdentity`, `SystemConfiguration` — a 1:1 match with Program.cs's module list (`ArchitectureFixture` plus the 8 application modules). No module csproj under `src/Modules/*/SquadCrm.Modules.*` other than these 9 (i.e., no `*.Contracts` or `*.Bootstrap` project) has a `Persistence/Migrations` directory.
4. Each of the 9 module directories follows a fixed convention (confirmed via `find … -iname "*DbContext.cs"`): the DbContext file lives at `src/Modules/<Module>/SquadCrm.Modules.<Module>/Persistence/<Module>DbContext.cs` and the class is named `<Module>DbContext` (e.g. `src/Modules/Audit/SquadCrm.Modules.Audit/Persistence/AuditDbContext.cs` → `AuditDbContext`). The project path and startup-project path passed to `dotnet ef` are always the same directory: `src/Modules/<Module>/SquadCrm.Modules.<Module>`.
5. `src/backend/README.md:307-326` ("Adding persistence to a new module") — step 5 requires every module to ship its own `IDesignTimeDbContextFactory` reading the process environment via the shared `SquadCrm.Infrastructure.Postgres` implementation, and step 7 requires every new module's migrations to land under `Persistence/Migrations` in that same module project. This is why scanning for `Persistence/Migrations` directories is a reliable, structurally-enforced signal for "this module has migrations to apply" — it is not a coincidence of the current 9 modules, it is how new modules are required to be built.
6. `README.md:247` (`| \`scripts/migrate\` | Apply every current module migration | available today |`) and `README.md:130-136` (full-stack section, currently says to run `dotnet ef database update --project … --context …` per `src/backend/README.md` "once" after first start) and `README.md:258-296` ("Migrations and tests" section, steps 1-4 show only the `ArchitectureFixture` `dotnet ef` invocation as the example, then introduces `scripts/migrate` at line 290 as the wrapper). These already *claim* `scripts/migrate` covers every module — the code just doesn't yet. Update the narrative in lines 130-136 and 258-290 so the manual per-module example is clearly labeled as illustrative (one example, not the full list) and no longer implies a developer must repeat it per module.
7. `src/backend/README.md:266-292` ("Applying migrations") — same illustrative single-module example (`ArchitectureFixture`) with a `dotnet ef migrations list` confirmation step; no changes required here beyond optionally noting `scripts/migrate` from the repo root as the multi-module wrapper (already true, not misleading — leave as-is unless it explicitly claims only `ArchitectureFixture` is covered, which it does not).

---

## Implementation tasks

### 1 — Discover and migrate every application module by directory scan

**File: `scripts/migrate`**

Replace lines 22-30 (the `dotnet tool restore` call stays; the two hardcoded `dotnet ef database update` blocks are replaced) with a loop that discovers modules from `Persistence/Migrations` directories and applies each one, keeping `ArchitectureFixture` as an explicit, separately-labeled first step:

```bash
cd "$repo_root/src/backend"
dotnet tool restore

apply_module_migrations() {
  local module_name="$1"
  local module_dir="$2"
  local context="${module_name}DbContext"
  echo "==> Applying ${module_name} migrations (${context})"
  dotnet ef database update \
    --project "$module_dir" \
    --startup-project "$module_dir" \
    --context "$context"
}

# ArchitectureFixture is a test/fixture module, not an application module —
# applied first and named explicitly, not discovered by the loop below.
apply_module_migrations "ArchitectureFixture" \
  "src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture"

# Application modules: discovered from the repository structure so this list
# cannot silently drift from the modules that actually own migrations —
# every module directory with a Persistence/Migrations folder is applied.
# See src/backend/README.md ("Adding persistence to a new module") for the
# convention this scan relies on.
shopt -s nullglob
for migrations_dir in src/Modules/*/SquadCrm.Modules.*/Persistence/Migrations; do
  module_dir="${migrations_dir%/Persistence/Migrations}"
  module_name="$(basename "$module_dir")"
  module_name="${module_name#SquadCrm.Modules.}"
  [[ "$module_name" == "ArchitectureFixture" ]] && continue
  apply_module_migrations "$module_name" "$module_dir"
done
shopt -u nullglob
```

Keep `set -euo pipefail` at the top of the file unchanged (already present, line 2) — it is what makes `apply_module_migrations`'s `dotnet ef database update` failing propagate immediately as a non-zero script exit (fail-fast, per the story's acceptance criteria), without adding any explicit `|| exit 1` per call.

Do **not** sort or hardcode the resulting module name list anywhere else in the script — the `for migrations_dir in …` glob already iterates in a stable (lexicographic, per bash glob ordering) sequence, which is sufficient since each module's migrations are independent (different schema, different `__ef_migrations_history` table per `src/backend/README.md:258-264`) and application order between application modules does not matter.

### 2 — Update root README narrative

**File: `README.md`**

- Lines 130-136: reword so the sentence no longer instructs "apply module migrations from the host once — `dotnet ef database update --project … --context …` per module" as the documented step; point at `scripts/migrate` instead, e.g.: "After the first `docker compose up --build` against a fresh `squadcrm-pgdata` volume, run `scripts/migrate` from the repository root to apply every current module's migrations before exercising data-backed features. The `backend` service does not run migrations automatically."
- Lines 258-296 ("Migrations and tests"): keep the single-module `ArchitectureFixture` example (steps 1-4) as an illustration of the underlying `dotnet ef` mechanics — add one sentence before or after it clarifying it is one representative example, and that `scripts/migrate` (introduced at the existing line 290) is what a developer actually runs to cover every module. Do not remove the illustrative example; do not enumerate all 9 modules by name in prose (that would recreate the stale-list problem inside documentation).
- Line 241 (`Common commands` table row for the manual `dotnet ef database update` command) — keep the row, but confirm its "What it does" wording ("Apply one module's migrations") does not conflict with the corrected `scripts/migrate` row directly below it (line 247, "Apply every current module migration") — no change needed if the wording already reads consistently; verify while editing.

**File: `src/backend/README.md`**

No required change — this file's "Applying migrations" section already presents `ArchitectureFixture` explicitly as an illustrative single-module example (not a claim of full coverage), and does not claim `scripts/migrate` is incomplete. Reread lines 266-292 after Task 1 lands and confirm no sentence there implies `scripts/migrate` only covers `ArchitectureFixture`/`StaffIdentity` — if one is found, correct it the same way as the root README.

---

## Edge Cases & Failure Modes

- **A module directory exists but has an empty `Persistence/Migrations` folder (no migrations yet added)** — `dotnet ef database update` against an empty migrations set is a documented no-op (nothing to apply); the loop still calls it, which is harmless and keeps the discovery logic simple (no special-casing "empty" vs "has migrations").
- **One module's `dotnet ef database update` fails (e.g. bad connection, conflicting manual schema change)** — `set -euo pipefail` (already at `scripts/migrate:2`) aborts the script immediately at that `apply_module_migrations` call; later modules are not attempted; the script's exit code is `dotnet ef`'s non-zero exit code. This satisfies "fail fast" and "non-zero exit code on failure."
- **Rerunning `./scripts/migrate` after a full successful run** — `dotnet ef database update` for every module reports "No migrations were applied" (each module's own `__ef_migrations_history` table, per `src/backend/README.md:258-264`, already reflects the applied set) — no duplicate rows, no error, exit code `0`. This is the idempotency acceptance criterion.
- **A future module is added under `src/Modules/<New>/SquadCrm.Modules.<New>/` following the existing "Adding persistence to a new module" convention (`src/backend/README.md:307-326`)** — as soon as it has a `Persistence/Migrations` directory the glob picks it up automatically; nothing in `scripts/migrate` needs to change. This is the DX property the story exists to establish.
- **A module directory exists without a `Persistence/Migrations` folder (e.g. a module with no persistence, or a `.Contracts`/`.Bootstrap` project)** — excluded by the glob pattern itself (`src/Modules/*/SquadCrm.Modules.*/Persistence/Migrations` only matches directories that actually contain that path), so no extra filtering logic is needed and no such project is ever passed to `dotnet ef`.
- **`env/backend.env` missing (fresh clone)** — unchanged existing behavior (`scripts/migrate:9-19`): the five `POSTGRES_*` vars fall back to their existing developer-safe defaults; this story does not touch that block.

---

## Test Plan

This is a bash script with no existing automated test suite (`scripts/` has no test harness in the repo). Verification is manual/functional, per the Verification Steps below — no unit test file to add or modify. Confirm no `tests/` project references `scripts/migrate` by content (`grep -rn "scripts/migrate" src/backend/tests` before and after — expect no match either way, confirming this is correctly out of the automated test suite's scope).

---

## Verification Steps

1. **Fresh volume:** from the repository root, `docker compose down -v` (DESTRUCTIVE — only if no local data is being kept) then `export COMPOSE_ENV_FILES=env/backend.env && docker compose up -d postgres` and wait for `docker compose ps` to show `healthy`.
2. **First run:** `./scripts/migrate` — confirm exit code `0` and console output showing an `==> Applying …` line for `ArchitectureFixture` and for all 8 application modules (`Audit`, `BranchManagement`, `BrandingManagement`, `CustomerManagement`, `DepartmentManagement`, `RoleManagement`, `StaffIdentity`, `SystemConfiguration`).
3. **Confirm schemas:** `docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c '\dn'` lists a schema per migrated module (`architecture_fixture`, `audit`, `branch_management`, `branding_management`, `customer_management`, `department_management`, `role_management`, `staff_identity`, `system_configuration`, per `src/backend/README.md` schema-per-module convention).
4. **Idempotency:** run `./scripts/migrate` again — confirm exit code `0` and `dotnet ef` output for each module states no pending migrations (no duplicate rows, no error).
5. **Fail-fast check (optional but recommended):** temporarily stop `postgres` (`docker compose stop postgres`), run `./scripts/migrate`, confirm it exits non-zero at the first `dotnet ef database update` call and does not continue past it; restart `postgres` before continuing.
6. **StaffIdentity bootstrap:** `dotnet run --project src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap -- agent@example.test` (per `README.md:304-320`).
7. **First-admin bootstrap (CRM-206):** `dotnet run --project src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap -- --subject-email agent@example.test --role-code ADMINISTRATOR --role-name Administrator` (per `README.md:339-361`).
8. **Login + representative protected call:** start the API (`dotnet run --project src/backend/src/Api/SquadCrm.Api` or full `docker compose up --build`), log in as `agent@example.test` via `POST /api/v1/auth/login`, then confirm a protected endpoint such as `GET /api/v1/roles` returns `200` with the returned token.
9. **Formatting:** `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` (only `scripts/migrate`, a shell script, and Markdown changed — this confirms no accidental whitespace/formatting drift was introduced in touched `.cs`/`.md`-adjacent build files; expect a clean pass since no `.cs` file changes). `shellcheck scripts/migrate` if `shellcheck` is available locally (not a repo-enforced gate today, but a useful local check for the new loop).

---

## Done Criteria

- [x] `./scripts/migrate` applies migrations for all 9 modules with a `Persistence/Migrations` directory (`ArchitectureFixture` plus the 8 application modules), discovered from the repository structure — not a second hardcoded list.
- [x] Re-running `./scripts/migrate` against an already-migrated database exits `0` with no duplicate `__ef_migrations_history` rows for any module.
- [x] A failure in any single module's `dotnet ef database update` call stops the script immediately with a non-zero exit code; later modules are not attempted.
- [x] `env/backend.env` sourcing and the `POSTGRES_*` default-export block (`scripts/migrate:1-20`) are unchanged.
- [x] No new EF Core migration was added; no `DbContext`, entity, or module business logic file was changed.
- [x] The database is never reset/dropped and no staff/admin account is created by `scripts/migrate`.
- [x] `README.md` (lines 130-136 and the "Migrations and tests" section) no longer implies a developer must run per-module `dotnet ef database update` commands to get a usable local database; `scripts/migrate` is documented as the single command that covers every module.
- [x] `.squad/plans/cover-migrate-all-modules/00-overview.md` updated with this story's row.

## Verification evidence

Performed against the existing local Compose PostgreSQL (not a wiped volume — the
running database held prior work; wiping it was avoided per the story's own
"do not automatically reset/drop the database" constraint and general safety policy
around destructive operations). Coverage and idempotency are independent of whether
the volume started empty or already-migrated:

1. `./scripts/migrate` printed `==> Applying …` for all 9 modules: `ArchitectureFixture`,
   `Audit`, `BranchManagement`, `BrandingManagement`, `CustomerManagement`,
   `DepartmentManagement`, `RoleManagement`, `StaffIdentity`, `SystemConfiguration`.
   Exit code `0`.
2. `\dn` on the target database lists all 9 corresponding schemas plus `hangfire`/`public`
   (unrelated, pre-existing).
3. Re-running `./scripts/migrate` reported "No migrations were applied. The database is
   already up to date." for every module; exit code `0`.
4. Fail-fast check: stopped the `postgres` Compose service, ran `./scripts/migrate` —
   it failed at the first module (`ArchitectureFixture`) with a Postgres connection
   error and exit code `1`; no later module was attempted. Restarted `postgres`,
   waited for `healthy`, reran `./scripts/migrate` — exit code `0`, idempotent.
5. `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes` — clean,
   exit code `0`. `shellcheck` not installed locally — skipped (not a repo-enforced gate).
6. StaffIdentity bootstrap for `agent@example.test` — `Local staff account is ready.`
7. CRM-206 first-admin bootstrap (`--subject-email agent@example.test --role-code
   ADMINISTRATOR --role-name Administrator`) — `Authorization bootstrap completed.`
8. Started `SquadCrm.Api`, `POST /api/v1/auth/login` for `agent@example.test` returned
   an access token; `GET /api/v1/roles` with that token returned `200`.

**STOP HERE. Report to the user and wait for confirmation before implementation begins.**
