# Plan — CRM-143 Manage Agent Tasks

Branch: `feat/crm-143-manage-agent-tasks`

Full-stack. Mirrors the `TicketManagement` module shape (see
`SquadCrm.Modules.TicketManagement`) — no new architectural pattern.

## Backend — new module `SquadCrm.Modules.AgentTaskManagement`

`src/backend/src/Modules/AgentTaskManagement/SquadCrm.Modules.AgentTaskManagement/`

- `AgentTaskManagementModule.cs`: `IModule`, minimal-API endpoints, registers
  `DbContext` with schema `agent_task_management` +
  `AgentTaskManagementOutboxInterceptor`.
- `Permissions.cs`: `tasks.view`, `tasks.create`, `tasks.edit`,
  `tasks.complete` (+ internal `PermissionPolicies` `"permission:tasks.*"`).
- `Persistence/AgentTask.cs`: entity — `Id`, `Title`, `Details?`,
  `OwnerUserId`, `TicketId?`, `CustomerId?`, `DueAtUtc?`, `Status`
  (`Open`/`Completed`), `CompletedAtUtc?`, `CreatedAtUtc`, `UpdatedAtUtc`.
  Extends `HasDomainEvents`.
- `Persistence/AgentTaskManagementDbContext.cs` /
  `AgentTaskManagementSchema.cs` (schema `agent_task_management`,
  `__ef_migrations_history`) / `AgentTaskManagementDbContextFactory.cs`
  (design-time) — snake_case mapping, `Status` `.HasConversion<string>()`,
  `Ignore(t => t.DomainEvents)`.
- `Persistence/OutboxMessage.cs` + `OutboxMessageConfiguration.cs` +
  `AgentTaskManagementOutboxInterceptor.cs` — copy the TicketManagement
  interceptor shape; `switch` on domain event type, throw on unmapped type.
- `Events/`: `AgentTaskCreatedDomainEvent`, `AgentTaskCompletedDomainEvent`,
  `AgentTaskReopenedDomainEvent` + matching `*IntegrationEvent`s. Producer
  only — no dispatcher (matches current repo-wide state; CRM-144 consumes
  later).
- `AgentTaskContracts.cs`: `CreateAgentTaskRequest`, `UpdateAgentTaskRequest`,
  `AgentTaskResponse`, `AgentTaskListQuery`/`AgentTaskListResult` (paging +
  filter by status/owner-mine/due-range + sort).
- `AgentTaskService.cs`:
  - `CreateAsync`: defaults `OwnerUserId` to caller
    (`ICurrentUserAccessor`) unless an eligible different owner is
    specified; validates `TicketId`/`CustomerId` existence via lookup
    contracts (below) when supplied — 404/400 on invalid link, does not
    grant access to the linked resource.
  - `ListAsync`: `myTasksOnly` flag → filter `OwnerUserId == currentUserId`
    resolved server-side (never trust a client-supplied id) — same pattern
    as `TicketService.ListAsync`'s `assignedToMe`.
  - `GetAsync`/`UpdateAsync`: only owner (or future permitted manager —
    out of scope now) can edit; 403 otherwise.
  - `CompleteAsync`: sets `Status = Completed`, `CompletedAtUtc = UtcNow`,
    raises `AgentTaskCompletedDomainEvent`.
  - `ReopenAsync`: explicit supported transition back to `Open`, clears
    `CompletedAtUtc`, raises `AgentTaskReopenedDomainEvent` (Business Rule:
    completed tasks are immutable except through this explicit workflow).
- Cross-module lookups consumed (existing contracts, no new project refs
  beyond `.Contracts`):
  - `ICustomerExistsLookup` (`CustomerManagement.Contracts`) — existence
    check for `CustomerId`.
  - New `ITicketExistsLookup` in `TicketManagement.Contracts` (does not
    exist yet — add it, mirroring `ICustomerExistsLookup`, implemented in
    `TicketManagementModule.RegisterServices`) — existence check for
    `TicketId`. Existence only; linking a task to a ticket does not check
    or grant ticket access (Business Rule).
- Endpoints (`RequireAuthorization(PermissionPolicies.X)`):
  `POST /tasks`, `GET /tasks` (list, `myTasksOnly` query flag), `GET
  /tasks/{id}`, `PUT /tasks/{id}`, `POST /tasks/{id}/complete`, `POST
  /tasks/{id}/reopen`.
- csproj references: `BuildingBlocks`, `Infrastructure.Postgres`,
  `CustomerManagement.Contracts`, `TicketManagement.Contracts`.
- Register module in host composition (wherever `TicketManagementModule` is
  registered alongside other modules).
- Migration: `Persistence/Migrations/20260911xxxxxx_InitialAgentTaskManagement.cs`.

## RoleManagement — permission catalog

- Add `TasksView`/`TasksCreate`/`TasksEdit`/`TasksComplete` to
  `Permissions.cs` (`Codes`/`Policies` const groups, same shape as the
  Ticket ones).
- New migration `AddAgentTaskPermissions.cs` (`InsertData`/`DeleteData` on
  `permission_definition`, module `"Agent Task Management"`), mirroring
  `AddTicketPermissions.cs`. No demo-data role assignment change needed
  beyond what already grants broad permissions to seeded roles (verify
  against `RoleManagementDemoDataContributor` at implementation time and
  add task permissions to the same roles that already hold ticket
  permissions, so demo agents can use the feature).

## Frontend — new `tasks` feature, mirrors `tickets`/`my-tickets`

`src/frontend/projects/agent-crm/src/app/tasks/`

- `tasks.service.ts`: typed HTTP client (`TaskListQuery`, `TaskSortBy`,
  `SortDirection`), matching `tickets.service.ts` shape.
- `task-list.ts/.html/.scss/.spec.ts`: general list (permission
  `tasks.view`), `p-table` lazy-load server paging, `p-select` filters
  (status, due range), `pInputText` search, `p-tag` status badge.
- `my-tasks.ts/.html/.scss/.spec.ts`: same list shape with
  `myTasksOnly: true` fixed (no client-supplied owner id) — direct analog
  of `my-tickets.ts`.
- `task-detail.ts/.html/.scss/.spec.ts`: title/details/owner/due/status,
  optional linked ticket (`routerLink="/tickets/:id"`) / customer
  (`routerLink="/customers/:id"`) shown only when present, Complete/Reopen
  actions gated by `tasks.complete`.
- `task-create/` folder: create form + own translations, linked
  ticket/customer picked via existing search patterns (reuse
  ticket/customer id inputs — no new search endpoint).
- `task-translations.ts`: flat dotted keys (`tasks.mine.title`,
  `tasks.fields.*`, `tasks.detail.*`), merged into the localization module
  same as `ticket-translations.ts`.
- Routes in `app.routes.ts`: `/tasks`, `/my-tasks`, `/tasks/new`,
  `/tasks/:id`, each `canActivate: [requirePermission('tasks.*')]`,
  lazy-loaded standalone components — mirrors the `tickets` route block.
- Nav entry for "My Tasks" alongside the existing "My Tickets" entry.

## Tests

- Backend: `AgentTaskService` unit tests (create/list/mine-filter/
  complete/reopen/link-validation-failure/ownership-403); EF Core
  migration applies cleanly; architecture test extended to cover the new
  module's dependency boundaries (schema-per-module, no cross-module
  DbContext access) if such a test already exists generically — verify at
  implementation time rather than assuming.
- Frontend: `task-list.spec.ts`, `my-tasks.spec.ts`, `task-detail.spec.ts`,
  `task-create` spec — list rendering/filtering, my-tasks-only filter,
  complete/reopen actions, linked ticket/customer rendering only when
  present.

## Verification

- Backend: `dotnet build`, `dotnet test` (new module + RoleManagement +
  affected architecture tests), EF Core migration check
  (`dotnet ef migrations has-pending-model-changes` or equivalent already
  used in repo).
- Frontend: `ng test` for new specs, `ng build`/lint.
- Browser smoke: create a task linked to a ticket and a customer, complete
  it, reopen it, confirm "My Tasks" only shows the caller's own tasks;
  check English/Arabic RTL layout.
