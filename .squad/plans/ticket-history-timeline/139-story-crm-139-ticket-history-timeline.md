# Plan — CRM-139 Ticket History Timeline

Branch: `feat/crm-139-ticket-history-timeline`

## Intake

- **Linear story:** CRM-139 "Ticket History Timeline" (Sprint 3 — Ticket
  Management, epic CRM-130). Blockers CRM-198, CRM-133, CRM-136, CRM-137 and
  CRM-138 are all Done and merged.
- **Goal:** a read-only chronological history timeline for one ticket —
  creation, assignment/reassignment, lifecycle status transitions and
  escalations — each entry carrying a safe event type, summary, actor/source
  and server timestamp, with stable ordering and pagination, plus an explicit
  customer-visible projection so a future portal cannot read internal history.
- **Linear's Deadline Acceptance Override is binding:** build the timeline
  from the simplest existing records/events. A generalized event-sourced
  timeline/read-model platform is stretch/non-blocking.
- **Reconciliation:** no `feat/crm-139-*` branch, no prior plan, no timeline
  service/endpoint/table in the tree. CRM-136/137/138 each wrote a
  purpose-specific append-only history table and explicitly deferred "the
  canonical ticket history timeline" to this story; each table already carries
  a `(TicketId, timestamp)` index annotated "CRM-139 consumes it". Clean start.

## Design decision — query-composed, no new table

The timeline is composed at query time from rows this module already owns
(`ticket`, `ticket_assignment_history`, `ticket_status_history`,
`ticket_escalation_history`), exactly like `CustomerTimelineService` (CRM-129)
does for customers. Reasons:

- The three history tables are already append-only, already written inside the
  same transaction as the mutation and its outbox row, and already indexed for
  this read. A second canonical `ticket_history` table would either duplicate
  every write (two sources of truth for the same fact) or require rewriting
  three merged stories' schemas — both worse than a projection.
- AC "as capabilities are added … events can appear without rewriting
  historical entries" is satisfied: a new capability adds its own history rows
  plus one projection arm; existing entries are never touched or migrated.
- This is the established project pattern (CRM-129), not a new one.

No migration. No new table. No new permission.

## Backend — `TicketManagement` module additions

`TicketContracts.cs`:

- `enum TicketTimelineActorType { User, System, Automation, Integration }` —
  full Fields Dictionary set; only `User` and `Automation` are produced today.
- `enum TicketTimelineVisibility { Internal, Customer }`.
- `enum TicketTimelineAudience { Internal, Customer }` — who is asking.
- `record TicketTimelineEntryResponse(Guid EventId, string EventType,
  DateTimeOffset OccurredAtUtc, int Sequence, TicketTimelineActorType ActorType,
  string? ActorId, string Summary, string? Reason,
  TicketTimelineVisibility Visibility)`.

`TicketTimelineService.cs` (new, mirrors `CustomerTimelineService` +
`TicketService` conventions):

- `GetAsync(ticketId, PaginationRequest, TicketTimelineAudience, ct)` →
  `TicketTimelineResult` with failure enum `{ None, TicketNotFound }`.
- Entries produced:
  - ticket row → `TicketCreated`, `EventId` = ticket id, `OccurredAtUtc` =
    `CreatedAtUtc`, `ActorType: User`, `ActorId: null`, visibility `Customer`.
  - `ticket_assignment_history` → `TicketAssigned` when `PreviousAgentId` is
    null, else `TicketReassigned`; `ActorType` from `Source`
    (`Manual` → `User`, `Automation` → `Automation`); `ActorId` = `ChangedBy`;
    visibility `Internal` (internal routing is not customer business).
  - `ticket_status_history` → `TicketStatusChanged`, `ActorId` = `ChangedBy`,
    `ActorType: User`, visibility `Customer`.
  - `ticket_escalation_history` → `TicketEscalated`, `ActorType` from `Source`,
    `ActorId` = `EscalatedBy`, visibility `Internal`.
- `Summary` is an allow-listed, secret-free string built from stable stored
  values only (statuses, levels, target type, ids) — never free text, never a
  reference-table lookup, so an entry still reads correctly after reference
  data changes (BR).
- `Reason` is the stored reason, carried separately from `Summary` so the
  customer projection can drop it wholesale.
- Ordering: `OccurredAtUtc` ascending, `EventId` as tie-breaker — total and
  deterministic, therefore stable across pages. `Sequence` is the 1-based
  index in that full ordering, assigned before paging so it identifies an
  entry independently of page size.
- Audience `Customer` filters to `Visibility == Customer` and nulls `Reason`
  before paging, so page numbering matches what that audience can actually see
  and internal entries are never counted or leaked.
- Merge and page in memory: all four sources are scoped to one ticket id and
  read `AsNoTracking`; per-ticket history volume is bounded by a single
  ticket's own activity, and an EF `Concat` across four differently-shaped
  tables buys no real benefit at that size.

`TicketManagementModule.cs`:

- `tickets.MapGet("/{id:guid}/history", GetTicketHistoryAsync)
  .RequireAuthorization(PermissionPolicies.TicketsView)` — reuses
  `tickets.view` (CRM-129 precedent: a read-only sub-resource of an entity
  reuses that entity's view permission). Handler takes `[AsParameters]
  PaginationRequest`, calls the service with `TicketTimelineAudience.Internal`,
  and reuses `NotFoundTicketProblem()`.
- `services.AddScoped<TicketTimelineService>()`.

The `Customer` audience has **no endpoint** in this story — the customer portal
is CRM-171+. The capability exists and is tested so that story consumes it
instead of writing its own filter over internal history.

## Frontend — `agent-crm` tickets feature

- `tickets.service.ts`: `TicketTimelineEntry` type + `history(id, page,
  pageSize)` returning `PagedResult<TicketTimelineEntry>`.
- `ticket-detail.ts`: `history`/`historyPage`/`historyTotal`/`historyUnavailable`
  signals, `loadHistory(page)`, called after the ticket loads and after every
  successful assign/status/escalate (those append entries). Failure is
  swallowed into `historyUnavailable` so a history error never blanks the
  ticket.
- `ticket-detail.html`: a "History" section rendering each entry as
  timestamp + localized event-type label + actor + summary (+ reason when
  present), with a PrimeNG `p-paginator` shown only when
  `historyTotal > pageSize`.
- `ticket-translations.ts`: en/ar keys for the section title, empty state,
  unavailable state, actor fallback (`System`) and each event type.

## Tests

- `SquadCrm.Persistence.IntegrationTests` (`TicketManagementTests`): creation
  entry present for a fresh ticket; assign/status/escalate each append the
  expected event type, actor and summary; ordering is chronological with a
  stable tie-break; paging returns disjoint pages whose union is the whole
  timeline and whose `Sequence` values are continuous; `Customer` audience
  excludes internal entries and nulls reasons; unknown ticket → `TicketNotFound`.
- `TicketEndpointsAuthorizationTests`: `GET /api/v1/tickets/{id}/history`
  requires `tickets.view`.
- `ticket-detail.spec.ts`: history renders, paginates and survives a failed
  history load.

## Scope gaps carried forward (documented, not silently dropped)

- **Customer-portal endpoint** — CRM-171/175. The audience-filtered projection
  is built and tested here; no portal route exists to expose it yet.
- **Internal notes / mentions / watchers timeline events** — CRM-147 adds its
  own history rows and one projection arm.
- **SLA and automation events** — CRM-150/152/154, same extension shape.
- **Conversation messages** — owned by CRM-164+. The timeline never duplicates
  message bodies (BR).
- **Creation actor** — CRM-133 does not persist a ticket creator column; the
  Audit module owns that record and belongs to another module. `TicketCreated`
  therefore reports `ActorId: null` rather than inventing an actor.
- **Material field changes beyond status/assignment/escalation** — no ticket
  edit capability exists in the repo yet; nothing to record.
