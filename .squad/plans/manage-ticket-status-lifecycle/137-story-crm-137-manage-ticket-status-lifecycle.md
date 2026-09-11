# Plan — CRM-137 Manage Ticket Status Lifecycle

## Intake

- **Linear story:** CRM-137 "Manage Ticket Status Lifecycle" (Sprint 3 — Ticket
  Management, epic CRM-130). Blockers CRM-198, CRM-133, CRM-135 are Done and
  merged.
- **Goal:** one authorized status-transition action on an existing ticket,
  validated against a fixed domain transition matrix, recorded in status
  history, emitting a durable event, protected by the existing
  optimistic-concurrency token.
- **Linear's Deadline Acceptance Override is binding:** a small fixed lifecycle
  (Open → In Progress/Pending → Resolved → Closed with sensible reopen) and
  recorded status history. Configurable lifecycle engines, transition DSLs,
  per-status policies and downstream choreography are stretch/non-blocking.
- **Reconciliation:** no `feat/crm-137-*` branch, no prior plan, no status
  transition endpoint/service method/history table/domain event in the tree.
  `TicketStatus` currently has the single value `Open`, explicitly documented
  as "later story's scope". `Ticket.Version`/`UpdatedAtUtc` and the CRM-136
  assignment slice give the exact pattern to follow. Clean start.

## Scope gaps carried forward (documented, not silently dropped)

- **SLA pause/continue semantics** (Pending Customer pauses resolution SLA,
  Pending Internal continues it) — no SLA model exists in this repo
  (CRM-149/150). The statuses are modelled so the SLA story can key off them;
  no SLA timer is invented here.
- **Escalation state** — BR says escalation is not a status; CRM-138/153/154
  own it. No escalation field is added.
- **Configurable lifecycle / per-status policy** — ruled out by the Deadline
  Acceptance Override. The transition matrix and the reason policy are fixed
  domain rules in one place (`TicketStatusTransitions`).
- **Notification / reporting / automation consumers** — CRM-156/188. Producer
  side only: the outbox row is written, no dispatcher/consumer exists yet.
- **Ticket history timeline UI** — CRM-139. This story writes status history
  rows; it renders no timeline.
- **Localized status labels** — the existing screens render the raw status
  token (same as `channel`); adding a label catalog is out of scope, but the
  action labels/messages this story adds are translated en/ar.

## Lifecycle

States: `Open`, `InProgress`, `PendingCustomer`, `PendingInternal`,
`Resolved`, `Closed`.

Allowed transitions (fixed):

- `Open` → `InProgress`, `PendingCustomer`, `PendingInternal`, `Resolved`
- `InProgress` → `PendingCustomer`, `PendingInternal`, `Resolved`
- `PendingCustomer` → `InProgress`, `PendingInternal`, `Resolved`
- `PendingInternal` → `InProgress`, `PendingCustomer`, `Resolved`
- `Resolved` → `Closed`, `InProgress` (reopen)
- `Closed` → `InProgress` (reopen)

A transition to the same status is not a transition (rejected as invalid), so a
double-submit cannot produce a duplicate history row or event.

Reason required for: any transition into `Closed`, and any reopen (from
`Resolved`/`Closed` back to `InProgress`). Optional otherwise (Fields
Dictionary: "Required for configured transitions such as reopen/close").

Reopen preserves history: the prior `Resolved`/`Closed` history rows stay,
a new row records the reopen (BR).

## Backend — module `TicketManagement`

- `Persistence/Ticket.cs` — extend `TicketStatus` with the five new values;
  add `ChangeStatus(TicketStatus target, string? reason, DateTimeOffset
  changedAtUtc)` — the single canonical mutation: sets `Status`,
  `UpdatedAtUtc`, increments `Version`, raises
  `TicketStatusChangedDomainEvent`. Mirrors `Assign`.
- `Persistence/TicketStatusTransitions.cs` — the fixed matrix
  (`IsAllowed`, `AllowedFrom`, `RequiresReason`), used by both the service
  (authoritative validation) and the detail projection (UX hint).
- `Persistence/TicketStatusHistory.cs` — new entity/table
  `ticket_status_history`: `Id`, `TicketId`, `PreviousStatus`, `NewStatus`,
  `Reason?` (max 500), `ChangedBy`, `ChangedAtUtc`; indexed by
  `(TicketId, ChangedAtUtc)`. Written in the SAME `SaveChanges` as the ticket
  update and the outbox row.
- `Events/TicketStatusChangedDomainEvent.cs` +
  `TicketStatusChangedIntegrationEvent.cs` (contract
  `ticket-management.ticket-status-changed.v1`) + the matching branch in
  `TicketManagementOutboxInterceptor.Translate`.
- `TicketContracts.cs` — `ChangeTicketStatusRequest(TicketStatus TargetStatus,
  string? Reason, int Version)`; `TicketDetailResponse` gains
  `AllowedStatusTransitions` (UX only — backend re-validates every call).
- `TicketService.ChangeStatusAsync` — load tracked ticket → 404; version
  mismatch → stale; transition not allowed → invalid transition; reason
  required and blank → reason required; then `ticket.ChangeStatus(...)`, add
  history row, single `SaveChangesAsync` (catch `DbUpdateConcurrencyException`
  → stale), audit `"status-changed"`.
- `TicketManagementModule.cs` — `POST /api/v1/tickets/{id}/status` with
  `.ValidatesDataAnnotations<ChangeTicketStatusRequest>()` and
  `.RequireAuthorization(PermissionPolicies.TicketsChangeStatus)`; problems:
  404 `tickets.not_found`, 409 `tickets.stale_version`, 422
  `tickets.invalid_status_transition`, 422 `tickets.reason_required`.
- New permission `tickets.changestatus` — this module's `Permissions.cs`,
  RoleManagement catalog + policy registration, seeded by a new RoleManagement
  migration (mirrors `AddTicketAssignPermission`).
- Migration `AddTicketStatusHistory` in TicketManagement. The `ticket.status`
  column is already `varchar(32)`, so the new enum values need no column
  change.

## Frontend — feature `tickets`

- `ticket-create.service.ts` — widen the `TicketStatus` union.
- `tickets.service.ts` — `changeStatus(id, request)`; `TicketDetail` gains
  `allowedStatusTransitions`.
- `ticket-detail.ts/html/scss` — a status-change inline form (same pattern as
  the assignment form), gated on `tickets.changestatus`, offering ONLY the
  statuses the backend reported as allowed, with a reason textarea required for
  close/reopen. 409 → "changed by someone else, reload"; 422 invalid transition
  → its own message. Reloads on success.
- `ticket-translations.ts` — en/ar keys for the status action, statuses and
  messages.

## Tests

- `TicketEndpointsAuthorizationTests` — anonymous `POST /{id}/status` → 401.
- `TicketManagementTests` — valid transition writes status, history row, bumped
  version and outbox row; invalid transition rejected and ticket unchanged;
  close without a reason rejected; reopen with a reason succeeds and preserves
  prior history; stale version rejected; unknown ticket → 404 failure.
- `ticket-detail.spec.ts` — action hidden without permission; only allowed
  transitions offered; submit posts the target status; 409 surfaces the
  stale-version message.

## Verification

- `dotnet build`; `dotnet test` for `SquadCrm.Api.Tests`,
  `SquadCrm.Persistence.IntegrationTests`, architecture tests.
- Migration check (persistence changed in two modules).
- Frontend `ng test` for the ticket specs, `ng build`, lint/format.
- Browser smoke: transition a ticket, Arabic/RTL intact, mobile width not
  broken.
