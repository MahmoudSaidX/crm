# Plan — CRM-133 Create Support Ticket

First story to add a `Ticket` entity to the existing `TicketManagement` module (do not
create a new module). Mirrors `Customer`'s create-with-organizational-scope-validation
shape (department/branch active lookups, generated immutable identifier, audit
recording) more than the simpler category/priority services, plus a new
module-local transactional-outbox producer since this is the first "Create X" story
with a real durable-event requirement (ADR-005). No Subcategory catalog/entity exists
in this repo yet (no CRM-131-equivalent "subcategories" story in the backlog) —
`SubcategoryId` is stored as a plain optional FK with no cross-validation until a
subcategory story exists; this is noted as a scope gap, not silently invented business
logic. Only the producer side of the outbox is built (interceptor + durable
`OutboxMessage` row in the same transaction) — no dispatch job/consumer, since no
downstream consumer (SLA/routing/reporting) is built yet (YAGNI); a later story adds
its own dispatcher the same way `ArchitectureFixtureOutboxJob` demonstrates.

## Backend — extend module `TicketManagement`

- `Persistence/Ticket.cs` — new entity extending `SquadCrm.BuildingBlocks.Events.HasDomainEvents`
  (first real consumer of that base class). Fields: Id, TicketNumber (string, generated,
  immutable), CustomerId (Guid), Subject, Description, CategoryId, SubcategoryId (Guid?),
  PriorityId, DepartmentId, BranchId, Status (enum: Open only for now — mirror
  `CustomerStatus` enum-in-entity-file style), Channel (enum: Agent/Portal/Email/
  WhatsApp/LiveChat/SMS/WebForm), AssignedAgentId (Guid?), CreatedAtUtc. A private/internal
  factory method or constructor call raises `TicketCreatedDomainEvent` (new
  `IDomainEvent` record in the same project, e.g. `Events/TicketCreatedDomainEvent.cs`)
  with TicketId/TicketNumber/CustomerId/OccurredAtUtc.
- `TicketManagementDbContext.cs` — add `DbSet<Ticket>` + Fluent config (mirror
  TicketCategory/TicketPriority column-naming style), unique index on TicketNumber,
  `ticket_management` schema. Register a new `TicketManagementOutboxInterceptor`
  (mirror `ArchitectureFixtureOutboxInterceptor.cs` exactly: snapshot
  `ChangeTracker.Entries<HasDomainEvents>()`, translate `TicketCreatedDomainEvent` →
  an internal `TicketCreatedIntegrationEvent : IIntegrationEvent` record (Type =
  `"ticket-management.ticket-created.v1"`), add an `OutboxMessage` row to the same
  tracker, `ClearDomainEvents()`) via `AddInterceptors` on `UseNpgsql` options in
  `TicketManagementModule.RegisterServices` (mirror how `ArchitectureFixtureDbContextOptions.Apply`
  wires its interceptor — inline `AddInterceptors` call here, no separate `*Options`
  helper needed for one interceptor).
- `Persistence/OutboxMessage.cs` + Fluent config — new table `ticket_outbox_message`
  in the same schema (mirror ArchitectureFixture's `OutboxMessage`/`OutboxMessageConfiguration`
  shape: Id, Type, Payload (json), OccurredAtUtc, CorrelationId). No claim/lease/processed
  columns yet — no dispatcher consumes this table in this story (YAGNI); a later story
  adds those columns via an additive migration when a real consumer exists.
- `TicketContracts.cs` — `CreateTicketRequest` (CustomerId, Subject, Description,
  CategoryId, SubcategoryId?, PriorityId, DepartmentId, BranchId, Channel,
  AssignedAgentId?) with DataAnnotations (`[Required]`, `[MaxLength]` per Fields
  Dictionary) + `TicketResponse` (all fields incl. TicketNumber, Status, CreatedAtUtc).
  `Channel` uses `[JsonConverter(typeof(JsonStringEnumConverter))]` (mirror
  `CustomerPreferredLanguage` handling in `CustomerContracts.cs`).
- `TicketService.cs` — `CreateAsync` only (no update/list/get needed by this story —
  browse/view are CRM-134/135): validates CategoryId/PriorityId are active (new
  `ITicketCategoryActiveLookup`/`ITicketPriorityActiveLookup`? — **no**, TicketCategory/
  TicketPriority live in this SAME module/DbContext, so query them directly via
  `dbContext.TicketCategories`/`TicketPriorities`, no cross-module contract needed),
  validates DepartmentId/BranchId via existing `IDepartmentActiveLookup`/
  `IBranchActiveLookup` (mirror `CustomerService.CreateAsync` exactly), validates
  CustomerId exists via a new `ICustomerExistsLookup` contract (new
  `SquadCrm.Modules.CustomerManagement.Contracts` project — mirror
  `IDepartmentActiveLookup`'s shape: single method `Task<bool> ExistsAsync(Guid
  customerId, CancellationToken)`; register the implementation in
  `CustomerManagementModule.RegisterServices`, consume via `.Contracts` project
  reference only, per the established cross-module convention), validates
  SubcategoryId (if supplied) belongs to CategoryId — **no**, no Subcategory entity
  exists (see header note) — skip this check, store the value as-is. Generates
  TicketNumber (`$"TKT-{Guid.NewGuid():N}"[..12].ToUpperInvariant()`, mirror
  `CustomerService.GenerateCustomerNumber`), builds `Ticket`, `dbContext.Add`,
  `SaveChangesAsync` inside `try/catch (DbUpdateException) when (IsUniqueViolation)`
  (mirror exactly — TicketNumber race), audit-records via `IAuditRecorder` (action
  "created"). Mutation-failure enum: `TicketMutationFailure` { None, InvalidCustomer,
  InactiveCategory, InactivePriority, InactiveDepartment, InactiveBranch,
  DuplicateTicketNumber }.
- `Permissions.cs` — add `TicketsCreate`/`TicketsView` consts (module-local file, same
  convention as existing `TicketCategoriesView` etc.) + matching `PermissionPolicies`.
  `TicketsView` is added now (unused by this story's single POST endpoint) because
  RoleManagement's permission catalog convention seeds view+manage/create pairs
  together, and CRM-134/135 will need `tickets.view` — reusing the same migration now
  avoids a near-duplicate permission-seed migration next story; the endpoint itself
  only gates on `TicketsCreate`.
- `TicketManagementModule.cs` — add `services.AddScoped<TicketService>()`, endpoint
  group `/api/v1/tickets` with a single `MapPost("", CreateAsync)
  .ValidatesDataAnnotations<CreateTicketRequest>().RequireAuthorization(PermissionPolicies.TicketsCreate)`
  (mirror the create-endpoint handler shape of `CreateAsync`/`CreatePriorityAsync`,
  switching on `TicketMutationFailure` to the matching `Results.Problem` code).
- EF migration `AddTicket` (generated via `dotnet ef migrations add`, additive to the
  existing `TicketManagement` context).
- RoleManagement migration `AddTicketPermissions` seeding `tickets.create`/
  `tickets.view` into `role_management.permission_definition` (mirror
  `AddTicketPriorityPermissions` migration exactly).
- `SquadCrm.Modules.CustomerManagement.Contracts` — new project, `ICustomerExistsLookup`
  interface + `CustomerExistsLookup` implementation registered in
  `CustomerManagementModule.RegisterServices` (mirror `IDepartmentActiveLookup`/
  `DepartmentActiveLookup` pattern exactly — single-method contract, `AsNoTracking`
  `AnyAsync` implementation).

## Frontend — new feature `ticket-create`

Only a create form — no list/detail view (CRM-134/135 build those; this story's own
AC is "create a ticket", not "see it afterwards" beyond the create response).

- `src/frontend/projects/agent-crm/src/app/ticket-create/`
  - `ticket-create.service.ts` — `TicketRequest`/`Ticket` interfaces + `createAsync`
    POSTing `/api/v1/tickets` (mirror `ticket-categories.service.ts`'s HttpClient/
    `firstValueFrom` shape, single method only).
  - `ticket-create-form.ts/html/scss` + spec — PrimeNG reactive form: Customer
    (autocomplete/dropdown backed by the existing customer search endpoint), Subject
    (text), Description (textarea), Category/Priority (dropdowns backed by the
    existing `ticket-categories`/`ticket-priorities` list endpoints, active items
    only), Department/Branch (dropdowns backed by existing department/branch list
    endpoints), Channel (dropdown, default "Agent"), optional Assigned agent
    (dropdown, later-story eligible-agent list not built — leave as free-form Guid
    input or omit control entirely if no agent list endpoint exists; confirm during
    implementation and omit rather than fabricate an endpoint).
  - `ticket-create-translations.ts` (mirror `ticket-category-translations.ts` shape).
- Route `/tickets/new` in `app.routes.ts`, guarded by `requirePermission('tickets.create')`.
- Nav entry gated on `authorization.state.has('tickets.create')` in `agent-shell.ts`;
  translation key in `agent-translations.ts`.
- Register `TICKET_CREATE_TRANSLATIONS` in `app.config.ts`.

## Tests

- Backend: `SquadCrm.Api.Tests/TicketEndpointsAuthorizationTests.cs` — anonymous 401 for
  the single POST route (mirror `TicketPriorityEndpointsAuthorizationTests.cs` shape,
  one route only).
- Backend: `SquadCrm.Persistence.IntegrationTests/TicketManagementTests.cs` (mirror
  `CustomerManagementTests.cs`'s structure) — create succeeds + generates TicketNumber
  + records one "created" audit entry; invalid/unknown CustomerId rejected; inactive
  category/priority/department/branch each rejected; concurrent create with the same
  generated-number race path is not realistically triggerable (number is random per
  call, unlike code-based dedup) — skip that case, add instead: two ticket creates for
  the same customer both succeed with distinct TicketNumbers (no false-duplicate
  rejection). Also assert exactly one `OutboxMessage` row is written per successful
  create, with `Type == "ticket-management.ticket-created.v1"`.
- Frontend: `ticket-create-form.spec.ts` (mirror `ticket-category-form.spec.ts` — valid
  submit calls the service and navigates/resets; validation errors block submit;
  locale/RTL smoke).

## Verification

- `dotnet build` for the backend solution.
- `dotnet test` — `SquadCrm.Api.Tests`, `SquadCrm.Persistence.IntegrationTests`,
  `SquadCrm.ArchitectureTests` (new cross-module `.Contracts` reference must satisfy
  the existing module-boundary architecture tests).
- EF migrations for `TicketManagement` and `RoleManagement` applied cleanly via the
  integration test fixture's real `Database.MigrateAsync()`.
- Frontend `ng test` for the new spec; `ng build`/lint.
- Out of scope: ticket list/browse/detail UI, ticket status lifecycle transitions,
  SLA timers, automatic assignment/routing, escalation, outbox dispatch job/consumer,
  Subcategory catalog and its validation, notification generation.
