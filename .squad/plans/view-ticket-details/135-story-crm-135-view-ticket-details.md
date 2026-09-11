# Plan — CRM-135 View Ticket Details

## Intake

- **Linear story:** CRM-135 "View Ticket Details" (Sprint 3 — Ticket Management,
  epic CRM-130). Blockers CRM-133 (Create) and CRM-134 (Browse/Search) are Done
  and merged to `main`.
- **Goal:** one practical read-only ticket-details screen backed by a single
  `GET /api/v1/tickets/{id}` endpoint.
- **Linear's own Deadline Acceptance Override is binding:** no generalized
  composition/projection framework, no extra cross-module infrastructure. That
  directly settles the "who resolves display names" question below.
- **Reconciliation:** no `feat/crm-135-*` branch, no prior plan, no ticket
  detail endpoint or component in the tree. Clean start.

## Scope gaps carried forward (documented, not silently dropped)

Same gaps as CRM-133/CRM-134, unchanged by this story:

- **Subcategory** — no subcategory catalog/entity exists. `SubcategoryId` is
  displayed as the raw stored value only; no name resolution.
- **SLA / Escalation state** — no SLA or Escalation model exists anywhere in the
  repo. AC says "displayed when available"; it is not available, so no section
  is rendered and no placeholder model is invented.
- **Ticket history timeline** — CRM-139, not built here. AC requires history to
  "integrate with its canonical capability rather than duplicate data"; the
  canonical capability does not exist yet, so nothing is rendered and no
  ticket-local history table is created.
- **Customer-facing conversation / internal notes** — Sprint 8 (CRM-164+) and
  CRM-147. Same reasoning: no duplicate data structure is created here.
- **Lifecycle / assignment / escalation actions** — CRM-136/137/138. The detail
  view renders no action buttons yet; AC's "UI exposes permitted actions as
  those capabilities become available" is satisfied by those later stories,
  each of which authorizes independently on the backend.
- **Organizational-scope enforcement** — `ICurrentUserAccessor` still carries no
  scope model; CRM-123's customer detail and CRM-134's ticket list both gate on
  permission alone. This story follows the same precedent (permission gate
  only) rather than inventing scope infrastructure ahead of the story that
  defines it.

## Ownership decision — where display names are resolved

- `TicketCategory` / `TicketPriority` live in **this same module and DbContext**,
  so the detail endpoint resolves their Arabic/English names by direct query,
  including **inactive** rows. This satisfies BR "historical labels/values must
  not disappear merely because reference data was deactivated" — the lookup is
  by id with no `IsActive` filter, and `categoryIsActive`/`priorityIsActive`
  flags are returned so the UI can mark a value as no longer active.
- Customer, Department, Branch and assigned agent belong to **other modules**.
  Per the Deadline Acceptance Override and the module-ownership rule, the
  backend does **not** gain four new cross-module summary lookup contracts.
  The Angular detail screen composes those labels from each owning module's own
  already-existing API (`/api/v1/customers/{id}`, departments, branches), which
  the backend authorizes independently. This also satisfies BR "customer-
  sensitive fields are returned only when caller permission/scope permits":
  customer data is fetched under `customers.view` and the customer summary is
  simply omitted when the caller lacks it — the ticket itself still renders.
- No staff-user name resolution for `assignedAgentId`: the assignee is shown as
  the raw id (same gap the CRM-133 create form and CRM-134 list already carry —
  no eligible-agent lookup endpoint exists). Documented, not invented.

## Backend — extend module `TicketManagement`

- `Persistence/Ticket.cs` — add `UpdatedAtUtc` (`DateTimeOffset?`, null until an
  update story writes it) and `Version` (`uint`, mapped to the Postgres `xmin`
  system column as the concurrency token). Fields Dictionary requires both.
  `xmin` needs no new column, so this is the minimal way to expose a real
  concurrency token; AC's "concurrent modification is surfaced ... where an
  edit action uses the ticket version" is conditional and no edit action exists
  in this story — the token is provided for CRM-136/137 to enforce against.
- `Persistence/TicketManagementDbContext.cs` — map `updated_at_utc`; declare
  `Version` via `UseXminAsConcurrencyToken()`.
- Migration `AddTicketUpdatedAtAndConcurrencyToken` — adds the nullable
  `updated_at_utc` column only.
- `TicketContracts.cs` — add `TicketDetailResponse`: every `TicketResponse`
  field plus `UpdatedAtUtc`, `Version`, `CategoryArabicName`,
  `CategoryEnglishName`, `CategoryIsActive`, `PriorityArabicName`,
  `PriorityEnglishName`, `PriorityIsActive`, `PriorityRank`. Nullable name
  fields mean "the referenced row no longer exists" — the UI falls back to the
  raw id rather than showing a blank.
- `TicketService.cs` — add `GetDetailAsync(Guid id, CancellationToken)`
  returning `TicketDetailResponse?`: single `AsNoTracking` query over `Tickets`
  left-joined to `TicketCategories`/`TicketPriorities` by id **without** an
  `IsActive` filter.
- `TicketManagementModule.cs` — `tickets.MapGet("/{id:guid}", GetTicketAsync)
  .RequireAuthorization(PermissionPolicies.TicketsView)`; 404 problem with code
  `tickets.not_found`, mirroring the existing not-found problem helpers.

## Frontend — extend feature `tickets`

- `tickets.service.ts` — `TicketDetail` interface + `get(id)` GET
  `/api/v1/tickets/{id}`.
- `ticket-detail.ts/html/scss` + spec — read-only definition-list layout
  following `audit/audit-detail.*` (the established read-only detail precedent;
  `customer-detail` is a multi-story aggregate screen and is not the right
  shape here). Sections: identity/status, classification, routing/assignment,
  timestamps, customer summary. Category/priority render the locale-appropriate
  name with an "inactive" tag when the reference row is deactivated, and fall
  back to the raw id when the name is null. Customer/department/branch labels
  are resolved through their own services with a silent fallback to the raw id
  when the call fails or the caller lacks permission. Back link to `/tickets`.
- `ticket-list.html` — ticket number becomes a `routerLink` to
  `/tickets/{{ ticket.id }}` (the inert link CRM-134's plan flagged).
- Route `tickets/:id` in `app.routes.ts` guarded by
  `requirePermission('tickets.view')`, registered **after** `tickets/new` so the
  literal segment keeps priority.
- `ticket-translations.ts` — en/ar keys for the new labels.

## Tests

- Backend `TicketEndpointsAuthorizationTests.cs` — anonymous GET
  `/api/v1/tickets/{id}` returns 401.
- Backend `TicketManagementTests.cs` — detail returns the ticket with resolved
  category/priority names; detail still returns names when the category and
  priority were deactivated after ticket creation (the BR that matters most
  here); unknown id returns null.
- Frontend `ticket-detail.spec.ts` — loads the ticket by route id; renders the
  not-found state on failure; falls back to the raw id when a reference label
  cannot be resolved.

## Verification

- `dotnet build`; `dotnet test` for `SquadCrm.Api.Tests` and
  `SquadCrm.Persistence.IntegrationTests`.
- `dotnet ef migrations` check — persistence changed.
- Frontend `ng test` for the new spec, `ng build`, lint/format.
- Browser smoke: open a ticket from the list, verify fields render, Arabic/RTL
  layout intact, mobile width not broken.
