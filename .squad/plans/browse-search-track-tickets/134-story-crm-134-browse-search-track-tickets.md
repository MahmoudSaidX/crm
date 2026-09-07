# Plan — CRM-134 Browse, Search & Track Tickets

Adds a list/search endpoint + Angular list UI to the existing `TicketManagement`
module. Mirrors CRM-123 Customer browse/search shape (`*ListQuery` + `ListAsync`
with filter/sort/paginate + `customer-list.ts` component pattern) more than
inventing a new pattern. Scope gaps carried over from CRM-133 stay gaps here:
no Subcategory entity exists yet, so SubcategoryIds filtering is not built (AC's
"subcategory where available" — it is not available); no SLA/Escalation model
exists anywhere in the codebase, so those filters are omitted per BR ("SLA/
escalation filters become available only when those capabilities exist").
Organizational-scope enforcement: `ICurrentUserAccessor` (CRM-110's seam) still
carries no identity/scope model, and CRM-123's own Customer list endpoint does
not filter by caller scope either — this story follows that same established
precedent (permission gate only) rather than inventing new scope-enforcement
infrastructure ahead of CRM-110/111 defining it. This is a documented scope
gap, not silently dropped AC.

## Backend — extend module `TicketManagement`

- `TicketContracts.cs` — add `TicketSortBy` enum (TicketNumber, CreatedAtUtc —
  the only two orderable fields with existing indexed/simple columns; mirror
  `CustomerSortBy`), reuse existing `SortDirection` from CustomerManagement?
  **no** — modules don't cross-reference each other's plain enums without a
  contract; define a module-local `SortDirection` mirroring the same two-value
  shape (existing precedent: `TicketChannel`/`TicketStatus` are also
  module-local copies, not shared). Add `TicketListQuery` record bound via
  `[AsParameters]` (mirror `CustomerListQuery` exactly): `Search?`,
  `Statuses[]?`, `CategoryIds[]?`, `PriorityIds[]?`, `AssigneeIds[]?`
  (nullable Guid array matching `AssignedAgentId`), `DepartmentIds[]?`,
  `BranchIds[]?`, `Channels[]?`, `SortBy = TicketNumber`,
  `SortDirection = Asc`.
- `TicketService.cs` — add `ListAsync(TicketListQuery query, PaginationRequest
  pagination, CancellationToken)` (mirror `CustomerService.ListAsync`):
  `AsNoTracking`, `Search` matches `TicketNumber.Contains` + `Subject.Contains`
  (case-sensitive is acceptable here — no normalized-search column exists on
  `Ticket` the way Customer has `NormalizedFirstName`; bound/parameterized via
  EF LINQ `Contains`, never string-concatenated SQL), then `Where` filters for
  each non-empty array param (`Statuses`, `CategoryIds`, `PriorityIds`,
  `AssigneeIds` against `AssignedAgentId`, `DepartmentIds`, `BranchIds`,
  `Channels`), deterministic sort switch (`TicketNumber`/`CreatedAtUtc`,
  asc/desc) `.ThenBy(t => t.TicketNumber)` stable tiebreaker (mirror exactly),
  skip/take pagination, return `PagedResult<Ticket>`.
- `TicketManagementModule.cs` — add `tickets.MapGet("", ListTicketsAsync)
  .RequireAuthorization(PermissionPolicies.TicketsView)` (the permission
  already exists, unused until now). Handler mirrors `ListAsync`/
  `ListPrioritiesAsync` shape: bind `[AsParameters] TicketListQuery`,
  `[AsParameters] PaginationRequest`, call service, map
  `PagedResult<Ticket>` → `PagedResult<TicketResponse>` via the existing
  `ToResponse(Ticket)`.
- No migration needed — no schema change, list-only story over the existing
  `ticket` table.

## Frontend — new feature `tickets`

- `src/frontend/projects/agent-crm/src/app/tickets/`
  - `tickets.service.ts` — `TicketListQuery`/`TicketListItem` interfaces +
    `list(query, page, pageSize)` GET `/api/v1/tickets` returning
    `PagedResult<Ticket>` (mirror `customers.service.ts` list method exactly,
    including query-param construction for array filters).
  - `ticket-list.ts/html/scss` + spec — PrimeNG `p-table` with `lazy`,
    `paginator`, `onLazyLoad` (mirror `customer-list.ts`/`.html` structure):
    search input; filter selects for Status, Category (backed by existing
    `ticket-categories` list endpoint, active items), Priority (backed by
    `ticket-priorities` list endpoint, active items), Department, Branch
    (backed by existing department/branch list endpoints), Channel (static
    enum options). No Assignee-name filter UI in this story — no
    "eligible/permitted users" list endpoint exists yet (same gap noted in
    CRM-133's plan for the create form's assignee control); omit rather than
    fabricate. Empty/loading/error states mirror `customer-list.html`'s
    `#emptymessage` + loading-signal pattern; row click navigates to
    `/tickets/{id}` (route/detail view is out of scope — CRM-135 — link left
    inert/no-op if no detail route exists, confirm during implementation).
  - `ticket-translations.ts` (mirror `customer-translations.ts` shape,
    en/ar).
- Route `/tickets` in `app.routes.ts`, guarded by
  `requirePermission('tickets.view')`.
- Nav entry gated on `authorization.state.has('tickets.view')` in
  `agent-shell.ts`; translation key in `agent-translations.ts`.
- Register `TICKET_TRANSLATIONS` in `app.config.ts`.

## Tests

- Backend: `TicketEndpointsAuthorizationTests.cs` — add anonymous 401 case for
  the new GET route (mirror existing POST case).
- Backend: `TicketManagementTests.cs` — list returns only matching
  status/category/priority/assignee/department/branch/channel filters;
  search matches ticket number and subject; default sort by TicketNumber is
  deterministic with stable tiebreaker; pagination boundaries (page 1 vs 2,
  page size respected); filters never expand result set beyond what an
  unfiltered call returns (BR: "filters cannot expand caller scope" — assert
  filtered subset ⊆ unfiltered set for the same query).
- Frontend: `ticket-list.spec.ts` (mirror `customer-list.spec.ts`) — loads
  page 1 on init; filter change resets to page 1 and reloads; empty state
  renders when zero results; loading state toggles around the service call.

## Verification

- `dotnet build` for the backend solution.
- `dotnet test` — `SquadCrm.Api.Tests`, `SquadCrm.Persistence.IntegrationTests`.
- Frontend `ng test` for the new spec; `ng build`/lint.
- Browser smoke: list loads, search/filters narrow results, pagination
  works, Arabic/RTL layout not broken.
- Out of scope: ticket detail view (CRM-135), Subcategory filtering, SLA/
  Escalation filtering, saved views, export, organizational-scope
  enforcement infrastructure (tracked as a pre-existing gap shared with
  CRM-123, not introduced by this story).
