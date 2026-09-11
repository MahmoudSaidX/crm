# Story 143 — Development Demo Data Seeder

Branch: `feat/crm-209-development-demo-data-seeder`, cut from `origin/main` after the Sakai UI redesign
(PR #44) and CRM-141 (PR #43) merged — the seeder work was implemented on top of `feat/sakai-ui-redesign`
but does not belong to that branch, so it was moved onto its own branch based on `main` before publication.

---

## Story Goal

`./scripts/seed-demo` makes a freshly migrated local Squad CRM database immediately usable
for development, demos, manual testing and UI verification, with realistic interconnected
data. It is explicitly invoked, refuses Production in code, is idempotent, respects module
ownership, and seeds only capabilities that exist today.

**Not in scope:** UI changes, new CRM capabilities, migrations, a generic seeding/CLI
framework, Faker, destructive reset, production bootstrap changes, startup seeding.

---

## Architecture inspection findings (the constraints this plan is built on)

1. **Modules and persistence owners** (`src/backend/src/Modules/*`): StaffIdentity, RoleManagement,
   Audit, DepartmentManagement, BranchManagement, CustomerManagement, TicketManagement,
   SystemConfiguration, BrandingManagement, ArchitectureFixture. Each owns one DbContext in
   `<Module>.Persistence` with a schema of its own and a public
   `*DbContextFactory : IDesignTimeDbContextFactory<>` that builds its connection string from
   environment variables (`Infrastructure.Postgres.ReadPostgresOptions`).
2. **Migration workflow**: `scripts/migrate` runs `dotnet ef database update` per module,
   discovering every `src/Modules/*/SquadCrm.Modules.*/Persistence/Migrations` directory. No
   startup migration. Permission catalog rows are inserted by RoleManagement migrations, so the
   authoritative live catalog is the `permission_definition` table.
3. **Existing bootstrap precedent**: `src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap`
   (`BootstrapProgram.EnsureDevelopment`, password from `SQUADCRM_BOOTSTRAP_STAFF_PASSWORD`,
   `PasswordHasher<StaffUser>`) and `src/Tools/SquadCrm.RoleManagement.Bootstrap`
   (`AuthorizationBootstrapService`, which grants the *live* `permission_definition` codes).
   Neither is modified by this story.
4. **Architecture test constraints**:
   - `ModuleProjectDependencyRulesTests`: a project under `src/Modules/**` may reference a foreign
     module only through `*.Contracts`. A project under `src/Tools/**` may reference module
     implementations — precedent: `SquadCrm.RoleManagement.Bootstrap`.
   - `PersistenceArchitectureRulesTests`: a module must not depend on another module's
     `*.Persistence` namespace; every DbContext must live in its owning module.
   - `CsprojDependencyRulesTests`: `BuildingBlocks.Abstractions` must declare **no**
     `FrameworkReference`/`PackageReference`/`ProjectReference`; `*.Contracts` projects may
     reference only Abstractions.
5. **Domain surfaces available to a contributor**:
   - `Ticket.Create/Assign/ChangeStatus/Escalate` are public and each take an **explicit
     timestamp**, bump `Version`, and raise domain events that
     `TicketManagementOutboxInterceptor` turns into `outbox_message` rows in the same save.
   - `TicketStatusTransitions.IsAllowed/RequiresReason` is the authoritative transition matrix.
   - `TicketService`/`CustomerService`/`StaffUserService` are `internal` **and** stamp
     `DateTimeOffset.UtcNow`, so they cannot produce a 90-day backdated history and cannot be
     called from another assembly. Therefore demo data must be produced **inside each owning
     module** through its own persistence + public domain methods — which is exactly the
     module-owned-contributor shape the intake requires.
6. **Constraints the generated data must respect**:
   - `customer` unique index `ix_customer_duplicate_match` on
     (`normalized_first_name`, `normalized_last_name`, `department_match_id`, `branch_match_id`)
     — plus unique `customer_number`. `DepartmentMatchId`/`BranchMatchId` are the non-nullable
     mirrors and must be set.
   - `customer_contact` partial unique index: at most one active primary contact **per type**
     per customer.
   - `CustomerNote.Body` ≤ 4000, `AuthorUserId` is a staff user id.
   - `ticket_number` unique; `Ticket.Description`/`Subject` non-null.
   - Escalation is invalid on `Resolved`/`Closed` (`TicketService.EscalateAsync` eligibility) —
     the contributor must apply the same rule.
   - `TicketStatusTransitions` never contains the current status, so no self-transition rows.
7. **Attachments**: `CustomerAttachment` rows point at bytes in `IFileStorage`, whose local root
   belongs to the API container (`src/Api/SquadCrm.Api/.data`). Seeding metadata from a host-run
   CLI would produce rows whose download fails. **Decision: seed no attachments** — the intake
   permits attachments "only where valid local storage behavior permits it". Documented in the
   seeder's README section and in the completion report as a deliberate deviation.

---

## Design

```
scripts/seed-demo
  └── dotnet run --project src/backend/src/Tools/SquadCrm.DemoDataSeeder -- [--size small|medium|large]
        DemoSeedProgram              environment guard, arg parsing, orchestration, summary
          └── IDemoDataContributor   (BuildingBlocks.Abstractions, pure contract)
                ├── StaffIdentityDemoDataContributor      (StaffIdentity module)
                ├── RoleManagementDemoDataContributor     (RoleManagement module)
                ├── DepartmentDemoDataContributor         (DepartmentManagement module)
                ├── BranchDemoDataContributor             (BranchManagement module)
                ├── TicketCatalogDemoDataContributor      (TicketManagement module)
                ├── CustomerDemoDataContributor           (CustomerManagement module)
                └── TicketDemoDataContributor             (TicketManagement module)
```

Each contributor opens **its own** module DbContext through that module's existing
`*DbContextFactory` and writes only its own tables through its own entities/domain methods. The
tool never touches a foreign DbContext. Cross-module references (staff ids, department ids,
customer ids, catalog ids) flow through a shared, module-published reference bag passed by the
orchestrator — not through foreign persistence.

### Shared contract (small and deliberately not a framework)

**New file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks.Abstractions/DemoData/DemoDataContracts.cs`**

Abstractions must stay dependency-free, so the contract is pure BCL:

```csharp
public enum DemoDataSize { Small, Medium, Large }

public sealed record DemoStaffReference(string Email, Guid Id, string DisplayName, string RoleCode);
public sealed record DemoCatalogReference(string Code, Guid Id);
public sealed record DemoCustomerReference(string CustomerNumber, Guid Id, Guid DepartmentId, Guid BranchId);

/// Mutable bag of references each contributor publishes for later contributors.
public sealed class DemoDataReferences
{
    public List<DemoStaffReference> Staff { get; } = [];
    public List<DemoCatalogReference> Departments { get; } = [];
    public List<DemoCatalogReference> Branches { get; } = [];
    public List<DemoCatalogReference> TicketCategories { get; } = [];
    public List<DemoCatalogReference> TicketPriorities { get; } = [];
    public List<DemoCustomerReference> Customers { get; } = [];
}

public sealed record DemoDataScope(
    DemoDataSize Size,
    int RandomSeed,
    DateTimeOffset NowUtc,
    DemoDataReferences References);

public sealed record DemoDataOutcome(string Contributor, IReadOnlyDictionary<string, int> Created);

public interface IDemoDataContributor
{
    string Name { get; }
    Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken);
}
```

`DemoDataSize` also carries the counts, kept in the tool (`DemoDataPlan`): small 20/50,
medium 150/400, large 1000/5000.

### Determinism and idempotency rules (applied by every contributor)

- One `Random(scope.RandomSeed)` per contributor, consumed in a fixed order — default seed
  `20260911`.
- Business keys, not hardcoded GUIDs: department/branch/category/priority `Code`, staff
  `NormalizedEmail`, customer `CustomerNumber` (`DEMO-C0001`…), ticket `TicketNumber`
  (`DEMO-T00001`…). All demo rows are prefixed `DEMO-`/`demo…`/`@squadcrm.local`, which is how
  the seeder "clearly identifies itself as demo data".
- Every contributor first loads existing rows by business key and inserts only what is missing;
  a ticket whose `TicketNumber` already exists is skipped whole, so its history is never
  re-applied. No `DELETE`/`TRUNCATE`/`DROP`, and non-demo rows are never read for mutation.
- Customer name pairs are generated so `(first, last)` never repeats (`first[i % F]`,
  `last[(i / F) % L]` with `F*L ≥ 1000`), which satisfies `ix_customer_duplicate_match`
  independently of department/branch.
- Absolute timestamps are derived from `scope.NowUtc` minus fixed offsets, so a rerun against the
  same database changes nothing (rows are skipped) while a fresh database always yields the same
  *relative* 90-day shape. Documented in the seeder section of the backend README.

### Environment guard (code-enforced)

**New file: `src/Tools/SquadCrm.DemoDataSeeder/DemoDataEnvironmentGuard.cs`**

`ASPNETCORE_ENVIRONMENT` must be exactly `Development` or `Test` (ordinal, allowlist). Anything
else — `Production`, `Staging`, unset, empty — throws `InvalidOperationException` before any
database connection is opened. Unit-tested. No configuration flag, env var or CLI switch can
override it.

---

## Tasks

### 1 — Shared demo-data contract

Create `DemoData/DemoDataContracts.cs` in `SquadCrm.BuildingBlocks.Abstractions` exactly as
above. Add no project/package/framework reference (`CsprojDependencyRulesTests` enforces this).

### 2 — StaffIdentity contributor

**New: `src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/DemoData/StaffIdentityDemoDataContributor.cs`**

- Public class implementing `IDemoDataContributor`, opening
  `new StaffIdentityDbContextFactory().CreateDbContext([])`.
- Five personas from the intake table; `Department`/`Branch` string fields set to the matching
  demo department/branch English names.
- Password: `Environment.GetEnvironmentVariable("SQUADCRM_DEMO_PASSWORD")` else the demo default
  `SquadDemo!2026`; hashed with `PasswordHasher<StaffUser>` (the existing hasher). The value is
  never logged, never returned in the outcome, and the local variable is cleared after use — same
  pattern as `BootstrapProgram`.
- Idempotent by `NormalizedEmail`: create when missing; when present, leave the existing password
  hash untouched (a developer may have changed it) but ensure `IsActive`, `DisplayName`,
  `Department`, `Branch`. Writes an `AuthenticationEvent` row (`user_created`, outcome
  `succeeded`, `ChangedByHandle = "demo-data-seeder"`) only for accounts it creates.
- Publishes `DemoStaffReference` for all five, including accounts that already existed.

### 3 — RoleManagement contributor

**New: `src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/DemoData/RoleManagementDemoDataContributor.cs`**

- Reads the **live** `permission_definition` codes (`dbContext.PermissionDefinitions`). No
  hardcoded catalog.
- Role composition is expressed as code *selectors* over that live catalog; codes not present in
  the database are skipped (never inserted), so a catalog change cannot produce an FK failure or
  a stale duplicate catalog:

  | Role (`Code` / `Name`) | Grants |
  | --- | --- |
  | `administrator` / Administrator | **every** code in `permission_definition` |
  | `support-manager` / Support Manager | `customers.view`, `customers.manage`, `departments.view`, `branches.view`, `ticketcategories.view`, `ticketpriorities.view`, `tickets.view`, `tickets.create`, `tickets.assign`, `tickets.changestatus`, `tickets.escalate` |
  | `support-agent` / Support Agent | `customers.view`, `customers.manage`, `ticketcategories.view`, `ticketpriorities.view`, `tickets.view`, `tickets.create`, `tickets.assign`, `tickets.changestatus`, `tickets.escalate` |
  | `read-only` / Read Only | `customers.view`, `tickets.view`, `ticketcategories.view`, `ticketpriorities.view`, `departments.view`, `branches.view` |

  Support Manager deliberately holds no `roles.*`, `users.*`, `audit.view`, `configuration.*`,
  `branding.*`, `*.manage` on catalogs. Read Only holds `*.view` only — no mutation code at all.
  `customers.manage` is included for agents because customer create/update/contacts/notes all
  sit behind that single permission in the current catalog (there is no finer-grained customer
  write permission to choose); this is recorded in the documented matrix.
- Roles created with `RoleService.Normalize` for `NormalizedName`/`NormalizedCode`, `IsActive`,
  `Description` marking them demo data. Idempotent by `NormalizedCode`; grants reconciled as a
  set difference (missing inserted, existing left alone, **nothing revoked**); assignments
  reconciled by `(StaffSubjectId, RoleId)`.
- Writes `RoleAuditEvent`/`PermissionChangeAuditEvent` rows for what it actually changes, mirroring
  `AuthorizationBootstrapService`. `AuthorizationBootstrapService` itself is **not** modified.
- Consumes `scope.References.Staff` for subject ids — no StaffIdentity persistence access.

### 4 — Department and Branch contributors

**New:** `.../DepartmentManagement/…/DemoData/DepartmentDemoDataContributor.cs`,
`.../BranchManagement/…/DemoData/BranchDemoDataContributor.cs`

- Departments (5 active): `customer-support`, `technical-support`, `billing`, `operations`,
  `customer-success`, each with Arabic + English names. Plus 1 inactive
  (`legacy-collections`) for filter/history testing.
- Branches (8 active): Riyadh (HQ + Olaya), Jeddah, Dammam, Makkah, Madinah, Khobar, Abha,
  with `code` like `riyadh-hq`; Arabic + English names. Plus 1 inactive (`taif-legacy`).
- Only the fields the model has (`Code`, `NormalizedCode`, `ArabicName`, `EnglishName`,
  `Description`, `IsActive`, timestamps) — no invented geographic columns.
- Idempotent by `NormalizedCode`. Publishes references (active ones only, for downstream use).

### 5 — Ticket catalog contributor

**New: `.../TicketManagement/…/DemoData/TicketCatalogDemoDataContributor.cs`**

- Categories (active, Arabic + English, `SortOrder`, `DefaultDepartmentId` pointing at the
  matching demo department where sensible): `account`, `billing`, `technical-issue`, `delivery`,
  `service-request`, `complaint`, `general-inquiry`. One inactive: `legacy-request`.
- Priorities using the existing `Rank` semantics (lower rank = more urgent, matching the existing
  rows if any are already present): `urgent` (1), `high` (2), `normal` (3), `low` (4), all
  active, Arabic + English names, description.
- Idempotent by `NormalizedCode`; publishes references.

### 6 — Customer contributor

**New: `.../CustomerManagement/…/DemoData/CustomerDemoDataContributor.cs`** (plus a small
`DemoCustomerNames.cs` name-pool file in the same folder)

- Count by size: 20 / 150 / 1000. `CustomerNumber` = `DEMO-C0001`… (≤ 32 chars, unique).
- Names from Arabic/English personal-name pools, paired for guaranteed uniqueness (see
  Determinism). `NormalizedFirstName`/`NormalizedLastName` produced with the same
  `value.Trim().ToUpperInvariant()` rule `CustomerService.Normalize` uses.
- `DepartmentId`/`BranchId` round-robin over the published active references, with
  `DepartmentMatchId`/`BranchMatchId` mirrors set (never left default when the id is set).
- `PreferredLanguage` alternates Arabic/English (a few left null); `Status` ≈ 85 % Active /
  15 % Inactive; `CreatedAtUtc` spread over the previous 90 days, `UpdatedAtUtc ≥ CreatedAtUtc`.
- Contacts: one primary Email (`demo.<first>.<last>.<n>@example.invalid`) and one primary Phone
  (`+9665xxxxxxxx`, deterministic digits) per customer — at most one active primary per type, so
  the partial unique index holds — plus a secondary non-primary email/phone on roughly a third of
  customers. Obviously synthetic; no real personal data.
- Notes: 0–3 per customer, realistic Arabic/English internal-note bodies (≤ 4000 chars),
  `AuthorUserId` from the demo staff references, `CreatedAtUtc` inside the customer's lifetime —
  these are what make the customer timeline non-empty.
- **No attachments** (see inspection finding 7).
- Idempotent by `CustomerNumber`; a customer that exists is skipped together with its contacts
  and notes. Publishes `DemoCustomerReference`.

### 7 — Ticket contributor (the important one)

**New:** `.../TicketManagement/…/DemoData/TicketDemoDataContributor.cs`,
`.../DemoData/DemoTicketScript.cs`, `.../DemoData/DemoTicketContent.cs`

- Count by size: 50 / 400 / 5000. `TicketNumber` = `DEMO-T00001`…
- Content: a fixed pool of realistic Arabic and English subject/description pairs matching the
  intake topics (account access, payment not reflected, delivery delayed, incorrect customer
  information, mobile application issue, service request follow-up, billing inquiry, account
  verification), combined with category/priority/channel so no two tickets read identically.
  Never `Ticket 1`/`Ticket 2`.
- `DemoTicketScript` is a pure, testable generator: given a ticket index it returns an ordered
  list of steps (`Assign(agent)`, `ChangeStatus(target, reason?)`, `Escalate(targetKind, …)`)
  with relative day/hour offsets. It **validates every status step against
  `TicketStatusTransitions.IsAllowed`** and supplies a reason whenever `RequiresReason` is true;
  it never emits an escalation step for a ticket already in `Resolved`/`Closed`. A generated
  script that violates either rule is a bug the unit test in Task 9 catches.
- Script mix (applied deterministically by index, non-uniform):
  - ~20 % stay `Open`, some of those unassigned (the "new queue").
  - ~30 % `Open → assign → InProgress`.
  - ~15 % `Open → assign → InProgress → PendingCustomer → InProgress`.
  - ~10 % `Open → assign → PendingInternal → InProgress`.
  - ~12 % `… → Resolved`.
  - ~8 % `… → Resolved → Closed` (with the required close reason).
  - ~5 % reassignment paths (`assign agent1 → escalate → reassign agent2 → InProgress`, reason
    supplied because an owner is being replaced).
  - Escalations on ~8 % of tickets overall: mostly level 1, a subset escalated twice (level 2)
    with a department target, the rest with an agent target (manager or the other agent).
  - Resulting status distribution is queue-realistic: more Open/InProgress than Closed, and all
    six statuses present.
- Assignment targets: agent1, agent2, manager, and unassigned — biased so **each agent ends with
  ≥ 30 currently assigned tickets at medium** (the plan computes the split from the size, so small
  is proportionally lower and large much higher; the assertion for AC 9 is on medium).
- Timestamps: `CreatedAtUtc` spread across the previous 90 days (some created today), each
  subsequent step strictly later than the previous and never in the future, so `UpdatedAtUtc`,
  `Version` and history ordering are all coherent; a portion of tickets is updated today.
- Execution: `Ticket.Create(...)` then the script's steps through `ticket.Assign` /
  `ticket.ChangeStatus` / `ticket.Escalate` with the scripted timestamps, appending
  `TicketAssignmentHistory` / `TicketStatusHistory` / `TicketEscalationHistory` rows in the same
  change tracker (`ChangedBy`/`EscalatedBy` = the acting demo user's email) exactly as
  `TicketService` does. The outbox interceptor turns the raised domain events into
  `outbox_message` rows in the same save — outbox behavior is exercised, not bypassed.
- Saves in batches (e.g. 200 tickets) to keep the large size workable, each batch a single
  transaction; batch boundaries never split a ticket from its history.
- Idempotent by `TicketNumber`: existing numbers are loaded first and skipped entirely.

### 8 — The CLI tool

**New project: `src/backend/src/Tools/SquadCrm.DemoDataSeeder/`** —
`SquadCrm.DemoDataSeeder.csproj` (`OutputType=Exe`, `AssemblyName=SquadCrm.DemoDataSeeder`,
`RootNamespace=SquadCrm.Tools.DemoDataSeeder`, `FrameworkReference Microsoft.AspNetCore.App` for
the password hasher, `ProjectReference` to the seven module implementation projects it
orchestrates), `Program.cs`, `DemoSeedProgram.cs`, `DemoDataEnvironmentGuard.cs`,
`DemoDataPlan.cs`.

- `Program.cs` mirrors the existing tools: `await DemoSeedProgram.RunAsync(args, ct)`.
- `DemoSeedProgram.RunAsync`: guard → parse `--size` (default `medium`) and `--help` → build the
  contributor list **explicitly in dependency order** (staff, roles, departments, branches,
  ticket catalog, customers, tickets) → run them sequentially against one `DemoDataScope` → print
  a per-contributor created/skipped summary and the resulting ticket status/assignment
  distribution → return 0, or 2 with a clear message on `InvalidOperationException`.
- Banner: `DEVELOPMENT DEMO DATA — never run against production.` Never prints the password.
- No CLI framework, no DI container, no reflection-based contributor discovery.
- Register the project in `src/backend/SquadCrm.sln`.

### 9 — Tests

- `tests/SquadCrm.UnitTests`: `DemoDataEnvironmentGuardTests` — `Development`/`Test` pass;
  `Production`, `Staging`, `null`, `""`, `"development"` (wrong case) throw.
- `tests/SquadCrm.UnitTests`: `DemoTicketScriptTests` — for every size, every generated script:
  every status step satisfies `TicketStatusTransitions.IsAllowed`, a reason is present whenever
  `RequiresReason`, no escalation step follows `Resolved`/`Closed`, timestamps are strictly
  increasing and not in the future, and two runs with the same seed produce identical scripts.
  Requires `DemoTicketScript` to be public in `TicketManagement` (the unit-test project does not
  have `InternalsVisibleTo`) and `SquadCrm.UnitTests` to reference the TicketManagement project.
- `tests/SquadCrm.UnitTests`: `DemoCustomerNamesTests` — 1000 generated name pairs are unique
  (guards `ix_customer_duplicate_match`).
- `tests/SquadCrm.Persistence.IntegrationTests` (only if the suite already provides a live
  database fixture — inspect `DatabaseFixture` first; if it requires a running Postgres that CI
  does not provide, skip this and rely on the live verification below rather than adding a new
  CI dependency): seed `small` twice against a migrated database and assert row counts are
  unchanged on the second run.
- No new architecture test: `ModuleProjectDependencyRulesTests` already scans every csproj under
  `src/Modules/**` from disk, so the contributors are covered the moment they are added, and the
  tool lives under `src/Tools/**` where module-implementation references are allowed.

### 10 — `scripts/seed-demo`

New executable bash script, modelled on `scripts/migrate`:

```bash
#!/usr/bin/env bash
set -euo pipefail
# resolve repo root, source env/backend.env (SQUADCRM_BACKEND_ENV_FILE override), export POSTGRES_*
export ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Development}"
cd "$repo_root/src/backend"
dotnet run --project src/Tools/SquadCrm.DemoDataSeeder -- "$@"
```

Passes `--size` through. Does not touch `scripts/seed`, `scripts/migrate` or `scripts/reset`
(reset keeps owning the destructive path; extending it to call `seed-demo` is **not** part of
this story). `chmod +x`.

### 11 — Documentation

- `README.md` (root): add `scripts/seed-demo` to the command table; add the three-command fresh
  environment workflow (`docker compose up --build` → `./scripts/migrate` → `./scripts/seed-demo`)
  next to the existing migrate/seed section; add a clearly marked **DEVELOPMENT DEMO ACCOUNTS**
  section listing the five emails, the shared demo password and the `SQUADCRM_DEMO_PASSWORD`
  override, ending with **NEVER USE THESE CREDENTIALS IN PRODUCTION.**
- `src/backend/README.md`: a "Development demo data" section — what each contributor seeds, the
  documented role/permission matrix from Task 3, dataset sizes, determinism/idempotency
  semantics, the Production refusal, the no-attachments decision, and how to add a contributor
  for a future module (implement `IDemoDataContributor` in the owning module, add it to the
  ordered list in `DemoSeedProgram`).
- No ADR: no new cross-cutting architecture decision (a Tools-project operator command over
  module-owned contributors is the established pattern).

---

## Verification

Run from the repository root against a genuinely fresh volume:

```bash
scripts/reset --yes                 # fresh volume + migrate + existing fixture seed
scripts/seed-demo                   # medium
scripts/seed-demo                   # second run — must converge, no duplicates
ASPNETCORE_ENVIRONMENT=Production scripts/seed-demo   # must exit non-zero before connecting
scripts/seed-demo --size small      # size switch
cd src/backend
dotnet build SquadCrm.sln
dotnet test SquadCrm.sln
dotnet format SquadCrm.sln --no-restore --verify-no-changes
```

SQL checks (AC 4–13) via `docker compose exec -T postgres psql`: staff user count = 5; admin role
grant count = `count(*) from role_management.permission_definition`; viewer grants contain no
`.manage`/`.create`/`.assign`/`.changestatus`/`.escalate`; customer/contact/note counts; ticket
count and `group by status`; assigned counts per agent (≥ 30 each); non-zero
`ticket_assignment_history`, `ticket_status_history`, `ticket_escalation_history`, and
`outbox_message`; a reassignment (history rows with a non-null `previous_agent_id`) and a level-2
escalation exist.

Live HTTP smoke (AC 17) with the running stack: login as admin, agent1 and viewer; `GET`
customers list, tickets list, one ticket detail (timeline non-empty), agent1's assigned-tickets
endpoint; and one mutation as viewer expecting 403 where agent1 succeeds. No browser automation.

---

## Decision Gates / risks

- **Linear unreachable during intake** (`CONNECTION_CLOSED`) — the tracker id was a placeholder
  during planning/implementation, surfaced rather than silently resolved. Reconciled after the fact:
  Linear story [CRM-209](https://linear.app/mahmoud-said/issue/CRM-209/seed-realistic-demo-data-for-local-development)
  ("Seed Realistic Demo Data for Local Development") was created once connectivity returned, and this
  plan/intake were relabeled to it — the plan and implementation were not regenerated.
- If `SquadCrm.Persistence.IntegrationTests` has no usable live-database fixture, the idempotency
  assertion is covered by the live verification above instead of a new CI database dependency —
  report it rather than adding one.
- If the ticket count at `large` (5000 tickets with history and outbox rows) proves impractically
  slow in one run, keep the batching and report the timing; do not change the domain write path
  or bypass the outbox to make it faster.
