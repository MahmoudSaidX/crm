# Plan — CRM-141 Agent Assigned Tickets Dashboard

Branch: `feat/crm-141-agent-assigned-tickets-dashboard`

## Intake

- **Linear story:** CRM-141 "Agent Assigned Tickets Dashboard" (Sprint 4 —
  Agent Dashboard & Collaboration, epic CRM-140). Blockers CRM-110, CRM-117,
  CRM-134 and CRM-136 are all Done and merged.
- **Goal:** a "My Assigned Tickets" screen showing only the tickets currently
  assigned to the signed-in agent, with search/filter/sort, deterministic
  pagination, responsive presentation and clear empty/loading/error states.
- **Linear's Deadline Acceptance Override is binding:** reuse the existing
  ticket query. Queue prioritization, workload analytics, personalization and
  a dashboard framework are stretch/non-blocking and are not built here.
- **Reconciliation:** no `feat/crm-141-*` branch, no prior plan, no
  `my-tickets` route or component in the tree. `GET /api/v1/tickets`
  (CRM-134) already supports search, status/category/priority/assignee/
  department/branch/channel filters, allow-listed sort and pagination.

## Design decision — server-resolved `assignedToMe`, no client-supplied identity

"My tickets" is expressed as a boolean query flag `AssignedToMe`; the backend
resolves the agent id from `ICurrentUserAccessor.Handle` (the `sub` claim,
which is the staff user id) and filters `AssignedAgentId == currentUserId`.

- The frontend never sends an agent id, so the screen cannot be pointed at
  another agent's queue by editing a request.
- BR "derived from canonical Ticket `AssignedAgentId`" is satisfied literally:
  no separate assignment store, no new table, no migration, no new permission
  (`tickets.view` already governs the list endpoint).
- Fail-closed: an unauthenticated or unparsable handle yields an empty page,
  never an unfiltered list.
- `AssignedToMe` composes with the existing `AssigneeIds` filter as an
  additional `AND`, so neither filter can widen the other.

## Backend — `TicketManagement` module changes

`TicketContracts.cs`:

- `TicketListQuery` gains `bool AssignedToMe = false`.
- `TicketSortBy` gains `UpdatedAtUtc` — the queue's most useful ordering, and
  the story's Fields Dictionary lists `UpdatedAtUtc` as a sort/result field.
- `TicketResponse` gains `DateTimeOffset? UpdatedAtUtc` (Fields Dictionary
  result field; already persisted since CRM-137).

`TicketService.ListAsync`:

- when `AssignedToMe` is set, resolve the caller id and add the
  `AssignedAgentId == currentUserId` predicate, or return an empty page when
  the handle is missing/unparsable;
- add the `UpdatedAtUtc` sort branch, keeping the existing `TicketNumber`
  stable tiebreaker so paging never reorders.

## Frontend — `agent-crm`

- `tickets.service.ts`: `assignedToMe` in `TicketListQuery`, `'UpdatedAtUtc'`
  in `TicketSortBy`, `updatedAtUtc` on the list row type.
- `my-tickets.ts/.html/.scss` (new): the queue screen — search box, status /
  category / priority filters, sort control, PrimeNG `p-table` with lazy
  pagination, status/priority tags, row link to `/tickets/:id`, explicit
  loading / empty / error states and a manual refresh (persisted state is
  authoritative; no realtime channel exists in the repo yet).
- `app.routes.ts`: `my-tickets` route behind `requirePermission('tickets.view')`.
- `agent-shell.ts`: nav entry behind the same permission.
- `ticket-translations.ts`: en/ar keys for the screen.

## Tests

- `TicketManagementTests`: `assignedToMe` returns only the caller's tickets;
  it ignores an attacker-supplied `assigneeIds` widening attempt; an
  unauthenticated caller gets an empty page; `UpdatedAtUtc` sort is stable
  across pages.
- `my-tickets.spec.ts`: renders the caller's queue, paginates, filters, shows
  the empty state, and shows the error state when the list call fails.

## Scope gaps carried forward (documented, not silently dropped)

- **SLA and escalation-state indicators** — CRM-149/150/153/154 own SLA and
  automatic escalation. The AC marks these "when available"; the raw
  `escalationLevel` is shown, no SLA column exists yet.
- **Realtime updates** — no realtime transport exists in the repo (CRM-156+).
  The screen refreshes on demand; persisted state stays authoritative.
- **Customer-safe summary** — the list projection carries `subject`; no
  customer-summary field exists on the ticket model.
- **Organizational scope** — no organizational scope model exists yet
  (CRM-110 deferred it); assignment identity is the only ownership check
  available today.
