# Plan — CRM-131 Configure Ticket Categories

Straightforward CRUD story, first of the Ticket Management epic (CRM-130). Mirror the
CRM-118 Manage Departments module pattern exactly (same schema-per-module shape, same
endpoint/service/DI conventions), adding `DefaultDepartmentId` (nullable FK validated
through the existing `IDepartmentActiveLookup` contract — the same contract
CustomerManagement already consumes for its own `DepartmentId`) and `SortOrder` (int).
No Ticket entity exists yet — do not build ticket-side selection UI or invent one.

## Backend — new module `TicketManagement`

- `src/backend/src/Modules/TicketManagement/SquadCrm.Modules.TicketManagement/`
  - `Persistence/TicketCategory.cs` — Id, Code, NormalizedCode, ArabicName, EnglishName,
    DefaultDepartmentId (Guid?), SortOrder (int), IsActive, CreatedAtUtc, UpdatedAtUtc.
  - `Persistence/TicketManagementSchema.cs` — schema `ticket_management`.
  - `Persistence/TicketManagementDbContext.cs` + `TicketManagementDbContextFactory.cs`
    (mirror `DepartmentManagementDbContext`/`Factory` exactly).
  - `TicketCategoryContracts.cs` — `CreateTicketCategoryRequest`/`UpdateTicketCategoryRequest`
    (Code, ArabicName, EnglishName, DefaultDepartmentId?, SortOrder) + `TicketCategoryResponse`.
  - `TicketCategoryService.cs` — create/update/get/list/activate/deactivate; duplicate-code
    precheck + Postgres 23505 race handling (mirror `DepartmentService`); validates
    `DefaultDepartmentId` via injected `IDepartmentActiveLookup.IsActiveAsync` when non-null,
    returning a new `TicketCategoryMutationFailure.InactiveDepartment` case (mirror
    `CustomerService`'s `InactiveDepartment` handling — same failure shape, no invented UX);
    calls `IAuditRecorder.RecordAsync` per mutation.
  - `Permissions.cs` — `ticketcategories.view` / `ticketcategories.manage` consts + internal
    `PermissionPolicies` (module-local convention, matches every other module's `Permissions.cs`).
  - `TicketManagementModule.cs` — DbContext registration, `IDepartmentActiveLookup` resolved
    from existing DI registration (no duplicate registration, no project reference to
    DepartmentManagement's main project — only its `.Contracts` project), `/api/v1/ticket-categories`
    endpoint group (POST, GET list, GET by id, PUT, POST activate, POST deactivate) gated by
    `PermissionPolicies.TicketCategoriesView`/`TicketCategoriesManage`.
  - csproj referencing BuildingBlocks, Infrastructure.Postgres, Audit.Contracts,
    `DepartmentManagement.Contracts` (cross-module contract only, per
    `ModuleProjectDependencyRulesTests`).
  - EF migration `InitialTicketManagement` (generated via `dotnet ef migrations add`, not
    hand-written, so it matches the real model snapshot).
- Register `TicketManagementModule` in `Program.cs` module array (after
  `CustomerManagementModule`) and add project reference in `SquadCrm.Api.csproj`.
- Permission catalog: new RoleManagement migration `AddTicketCategoryPermissions` seeding
  `ticketcategories.view`/`ticketcategories.manage` into `role_management.permission_definition`
  (mirror `AddDepartmentPermissions` migration's `InsertData`/`DeleteData` shape exactly).

## Frontend — new feature `ticket-categories`

- `src/frontend/projects/agent-crm/src/app/ticket-categories/`
  - `ticket-categories.service.ts` (mirror `departments.service.ts`: list/get/create/update/
    activate/deactivate; `TicketCategory`/`TicketCategoryRequest` interfaces add
    `defaultDepartmentId: string | null` and `sortOrder: number`).
  - `ticket-category-list.ts/html/scss` + spec (mirror `department-list.*`; add a
    "Default department" column resolved from a loaded department list, matching
    `customer-form.ts`'s pattern of loading active departments into select options).
  - `ticket-category-form.ts/html/scss` + spec (mirror `department-form.*`; fields Code,
    Arabic name, English name, Sort order (number input), Default department
    (`p-select`, optional, sourced from `DepartmentsService.list`, filtered to active —
    mirror `customer-form.ts`'s `departmentOptions`)).
  - `ticket-category-translations.ts` (mirror `department-translations.ts`).
- Routes in `app.routes.ts`: `/ticket-categories`, `/ticket-categories/new`,
  `/ticket-categories/:id/edit`, guarded by
  `requirePermission('ticketcategories.view'|'ticketcategories.manage')`.
- Nav entry in `agent-shell.ts` gated on `authorization.state.has('ticketcategories.view')`;
  translation keys `agent.navigation.ticketCategories` in `agent-translations.ts`.
- Register `TICKET_CATEGORY_TRANSLATIONS` in `app.config.ts`.

## Tests

- Backend: `SquadCrm.Api.Tests/TicketCategoryEndpointsAuthorizationTests.cs` (mirror
  `DepartmentEndpointsAuthorizationTests.cs` — anonymous 401 coverage for all six routes).
- Backend: `SquadCrm.Persistence.IntegrationTests/TicketCategoryManagementTests.cs` (mirror
  `DepartmentManagementTests.cs` — create/update/activate/deactivate/duplicate-code (incl.
  whitespace/case variants + concurrent-race)/unknown-id/empty-description-equivalent
  coverage, plus one new case: create/update with an inactive `DefaultDepartmentId` returns
  `InactiveDepartment`, and with `null` succeeds).
  - `PostgresTestDatabase.cs`: add `using SquadCrm.Modules.TicketManagement.Persistence;`,
    a `CreateTicketManagementContext()` factory accessor (mirror
    `CreateDepartmentManagementContext()`), and a migrate call in `InitializeAsync` after
    `customerManagement.Database.MigrateAsync()`.
  - `SquadCrm.Persistence.IntegrationTests.csproj`: add project references to the new
    `SquadCrm.Modules.TicketManagement` project (mirror the existing DepartmentManagement
    reference block).
- Frontend: `ticket-category-list.spec.ts`, `ticket-category-form.spec.ts` (mirror the
  Department specs, including the "hides management actions until
  ticketcategories.manage is granted" and locale/RTL coverage).

## Verification

- `dotnet build` for the backend solution.
- `dotnet test` — `SquadCrm.Api.Tests`, `SquadCrm.Persistence.IntegrationTests`,
  `SquadCrm.ArchitectureTests` (module-boundary rules — no new registration needed there,
  they discover modules generically/via a fixed assembly list that does not yet enumerate
  every business module).
- `dotnet ef migrations` for both `TicketManagement` and `RoleManagement` applied cleanly
  against local Postgres (via the integration test fixture, which runs real
  `Database.MigrateAsync()` calls — no `dotnet ef database update` required separately).
- Frontend `ng test` for the new specs; `ng build`/lint.
- Out of scope for this story (do not build): Ticket entity, ticket creation/selection UI,
  ticket routing/assignment, subcategory trees, generalized taxonomy, routing metadata,
  dependency policies.
