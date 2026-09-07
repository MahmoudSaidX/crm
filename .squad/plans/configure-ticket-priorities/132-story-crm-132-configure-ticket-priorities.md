# Plan — CRM-132 Configure Ticket Priorities

Straightforward CRUD story, second of Ticket Management epic (CRM-130). Add to the
existing `TicketManagement` module (do not create a new module) alongside
`TicketCategory`, mirroring its service/endpoint/DbContext conventions. No Ticket entity
exists yet — do not build ticket-side selection UI.

## Backend — extend module `TicketManagement`

- `Persistence/TicketPriority.cs` — Id, Code, NormalizedCode, ArabicName, EnglishName,
  Rank (int), Description (string?), IsActive, CreatedAtUtc, UpdatedAtUtc (mirror
  `TicketCategory.cs`; no department FK).
- `TicketManagementDbContext.cs` — add `DbSet<TicketPriority>` + entity configuration
  (unique index on NormalizedCode, mirror TicketCategory's config), same
  `ticket_management` schema.
- `TicketPriorityContracts.cs` — `CreateTicketPriorityRequest`/`UpdateTicketPriorityRequest`
  (Code, ArabicName, EnglishName, Rank, Description?) + `TicketPriorityResponse`
  (mirror `TicketCategoryContracts.cs`).
- `TicketPriorityService.cs` — create/update/get/list/activate/deactivate; duplicate-code
  precheck + Postgres 23505 race handling (mirror `TicketCategoryService.cs` exactly,
  minus the department-lookup branch); calls `IAuditRecorder.RecordAsync` per mutation
  (AC requires priority changes audited — `TicketCategoryService` already does this per
  mutation, reuse identical call shape).
- `Permissions.cs` — add `TicketPrioritiesView`/`TicketPrioritiesManage` consts +
  matching `PermissionPolicies` entries (module-local convention, same file).
- `TicketManagementModule.cs` — add `services.AddScoped<TicketPriorityService>()` and a
  new `/api/v1/ticket-priorities` endpoint group (POST, GET list, GET by id, PUT, POST
  activate, POST deactivate) gated by the new permission policies (mirror the existing
  ticket-categories group in the same file).
- EF migration `AddTicketPriority` (generated via `dotnet ef migrations add`, additive to
  existing `TicketManagement` context, not hand-written).
- Permission catalog: new RoleManagement migration `AddTicketPriorityPermissions` seeding
  `ticketpriorities.view`/`ticketpriorities.manage` into
  `role_management.permission_definition` (mirror `AddTicketCategoryPermissions`
  migration exactly).

Rank uniqueness (BR: "unique or otherwise deterministically sortable"): validate Rank is
a positive integer via data annotation; do not enforce a DB-level unique constraint —
ties are broken deterministically by an ORDER BY Rank, Code in `ListAsync` (simplest
complete option, no invented rule engine).

## Frontend — new feature `ticket-priorities`

- `src/frontend/projects/agent-crm/src/app/ticket-priorities/`
  - `ticket-priorities.service.ts` (mirror `ticket-categories.service.ts`; `TicketPriority`/
    `TicketPriorityRequest` interfaces: code, arabicName, englishName, rank: number,
    description: string | null).
  - `ticket-priority-list.ts/html/scss` + spec (mirror `ticket-category-list.*`; column
    for Rank instead of Default department).
  - `ticket-priority-form.ts/html/scss` + spec (mirror `ticket-category-form.*`; fields
    Code, Arabic name, English name, Rank (number input), Description (optional
    textarea) — no department select).
  - `ticket-priority-translations.ts` (mirror `ticket-category-translations.ts`).
- Routes in `app.routes.ts`: `/ticket-priorities`, `/ticket-priorities/new`,
  `/ticket-priorities/:id/edit`, guarded by
  `requirePermission('ticketpriorities.view'|'ticketpriorities.manage')`.
- Nav entry in `agent-shell.ts` gated on `authorization.state.has('ticketpriorities.view')`;
  translation keys `agent.navigation.ticketPriorities` in `agent-translations.ts`.
- Register `TICKET_PRIORITY_TRANSLATIONS` in `app.config.ts`.

## Tests

- Backend: `SquadCrm.Api.Tests/TicketPriorityEndpointsAuthorizationTests.cs` (mirror
  `TicketCategoryEndpointsAuthorizationTests.cs` — anonymous 401 coverage for all six
  routes).
- Backend: `SquadCrm.Persistence.IntegrationTests/TicketPriorityManagementTests.cs`
  (mirror `TicketCategoryManagementTests.cs` — create/update/activate/deactivate/
  duplicate-code (incl. whitespace/case variants + concurrent-race)/unknown-id coverage;
  drop the inactive-department case, no equivalent field here).
- Frontend: `ticket-priority-list.spec.ts`, `ticket-priority-form.spec.ts` (mirror the
  ticket-category specs, including "hides management actions until
  ticketpriorities.manage is granted" and locale/RTL coverage).

## Verification

- `dotnet build` for the backend solution.
- `dotnet test` — `SquadCrm.Api.Tests`, `SquadCrm.Persistence.IntegrationTests`,
  `SquadCrm.ArchitectureTests`.
- EF migrations for both `TicketManagement` and `RoleManagement` applied cleanly via the
  integration test fixture (real `Database.MigrateAsync()` calls).
- Frontend `ng test` for the new specs; `ng build`/lint.
- Out of scope: Ticket entity, ticket creation/selection UI, SLA coupling, severity
  policy engine, rule metadata.
