# Plan — CRM-138 Escalate Ticket Manually

## Intake

- **Linear story:** CRM-138 "Escalate Ticket Manually" (Sprint 3 — Ticket
  Management, epic CRM-130). Blockers CRM-133, CRM-118, CRM-136, CRM-137 are
  Done and merged.
- **Goal:** one authorized manual escalation action on an existing ticket —
  required reason, validated target, server-derived level, recorded in
  escalation history, emitting a durable event, protected by the existing
  optimistic-concurrency token, and visible on ticket list/detail independently
  of lifecycle status.
- **Linear's Deadline Acceptance Override is binding:** a simple level plus
  required reason and optional target, visible on list/details and recorded in
  history. Multi-stage escalation models, complex target eligibility and policy
  engines are stretch/non-blocking.
- **Reconciliation:** no `feat/crm-138-*` branch, no prior plan, no escalation
  field/table/endpoint/event in the tree — only forward references in CRM-136/137
  comments. CRM-136 assignment and CRM-137 status lifecycle give the exact
  slice pattern to follow (canonical domain mutation + append-only history +
  outbox event + version check + permission + inline form). Clean start.

## Scope gaps carried forward (documented, not silently dropped)

- **Configurable escalation rules / policy engine / eligibility matrix** —
  CRM-153 owns automatic escalation rules; ruled out here by the Deadline
  Acceptance Override. The level is derived, not configured.
- **Automatic escalation execution** — CRM-154. `TicketService.EscalateAsync`
  is written as the canonical capability taking a
  `TicketEscalationSource`, so CRM-154 calls it with `Automation` rather than
  adding a second write path (BR "escalation uses the canonical escalation
  capability shared by manual and automatic escalation"). No scheduler here.
- **Notification / reporting / automation consumers** — CRM-156/187/188.
  Producer side only: the outbox row is written, no dispatcher/consumer exists.
- **De-escalation / clearing escalation** — not in this story's AC. Escalation
  level only moves up; a later story owns any reset.
- **Role/Queue escalation targets** — the Fields Dictionary lists "Agent,
  Department, Role/Queue or supported target". Only `Agent` and `Department`
  are supported, because those are the only target kinds this repo can validate
  today (`IStaffSubjectReferenceReader`, `IDepartmentActiveLookup`). No queue
  model exists; inventing one would be fabricated business logic.
- **Organizational scope on the target** — `ICurrentUserAccessor` still carries
  no organizational scope (same gap CRM-136 documented). Target validity is
  "active agent" / "active department"; the check is not weakened, it is simply
  the strongest one the existing contracts support.
- **Ticket history timeline UI** — CRM-139. This story writes escalation
  history rows; it renders no timeline.

## Escalation model

- `EscalationLevel` on the ticket: `0` = not escalated. Each successful manual
  escalation sets it to `current + 1`.
- **The level is server-derived, never client-supplied** — the same reasoning
  that keeps `Source` off the request in CRM-136: a client that could name its
  own level could corrupt the level sequence (AC "prevent unintended
  duplicate/escalation-level corruption"). The request carries the concurrency
  `Version` instead, so two concurrent escalations cannot both land.
- Current escalation state on the ticket: `EscalationLevel`,
  `EscalationTargetType`, `EscalationTargetId`, `EscalatedAtUtc`. Every
  escalation also appends a history row, so a ticket has many historical
  escalations while exposing one current state (BR).
- **Escalation is not a lifecycle status** (BR): `Status` is untouched by this
  action, and escalation renders in its own column/section.
- **Eligibility:** a `Resolved` or `Closed` ticket cannot be escalated — an
  already-finished ticket has nothing to escalate. Every other status is
  eligible.
- **Reason is always required** (Fields Dictionary "Yes"), max 500 chars.
- **Target is always required** for the supported target types: `Agent` needs
  an active staff user, `Department` needs an active department.

## Backend — module `TicketManagement`

- `Persistence/TicketEscalationHistory.cs` — `TicketEscalationTargetType`
  (`Agent`, `Department`), `TicketEscalationSource` (`Manual`, `Automation`),
  and the append-only entity/table `ticket_escalation_history`: `Id`,
  `TicketId`, `PreviousLevel`, `NewLevel`, `TargetType`, `TargetId`, `Reason`
  (max 500), `Source`, `EscalatedBy`, `EscalatedAtUtc`; indexed by
  `(TicketId, EscalatedAtUtc)`. Written in the SAME `SaveChanges` as the ticket
  update and the outbox row.
- `Persistence/Ticket.cs` — escalation state properties plus
  `Escalate(int newLevel, TicketEscalationTargetType targetType, Guid targetId,
  string reason, TicketEscalationSource source, DateTimeOffset escalatedAtUtc)`:
  the single canonical mutation — sets the escalation state and `UpdatedAtUtc`,
  increments `Version`, raises `TicketEscalatedDomainEvent`. Mirrors
  `Assign`/`ChangeStatus`.
- `Events/TicketEscalatedDomainEvent.cs` +
  `TicketEscalatedIntegrationEvent.cs` (contract
  `ticket-management.ticket-escalated.v1`, target type/source as NAMES for the
  same durability reason as CRM-137) + the matching branch in
  `TicketManagementOutboxInterceptor.Translate`.
- `TicketContracts.cs` — `EscalateTicketRequest(TicketEscalationTargetType
  TargetType, Guid TargetId, string Reason, int Version)`; `TicketResponse` and
  `TicketDetailResponse` gain `EscalationLevel`, `EscalationTargetType`,
  `EscalationTargetId`, `EscalatedAtUtc`.
- `TicketService.EscalateAsync(ticketId, request, source, ct)` — load tracked
  ticket → 404; version mismatch → stale; `Resolved`/`Closed` → not eligible;
  blank reason → reason required; target inactive/unknown → invalid target;
  then `ticket.Escalate(...)`, add history row, single `SaveChangesAsync`
  (catch `DbUpdateConcurrencyException` → stale), audit `"escalated"`.
- `TicketManagementModule.cs` — `POST /api/v1/tickets/{id}/escalate` with
  `.ValidatesDataAnnotations<EscalateTicketRequest>()` and
  `.RequireAuthorization(PermissionPolicies.TicketsEscalate)`; problems: 404
  `tickets.not_found`, 409 `tickets.stale_version`, 422
  `tickets.not_escalatable`, 422 `tickets.invalid_escalation_target`, 422
  `tickets.reason_required`.
- New permission `tickets.escalate` — this module's `Permissions.cs`,
  RoleManagement catalog + policy registration, seeded by a new RoleManagement
  migration (mirrors `AddTicketChangeStatusPermission`).
- Migration `AddTicketEscalation` in TicketManagement (ticket columns + the
  history table).

## Frontend — feature `tickets`

- `ticket-create.service.ts` — `Ticket` gains the escalation fields.
- `tickets.service.ts` — `escalate(id, request)`; `TicketDetail` gains the
  escalation fields.
- `ticket-list.html/ts` — an escalation column rendering the level (a PrimeNG
  tag when escalated, otherwise the "not escalated" label), independent of the
  status column.
- `ticket-detail.ts/html/scss` — an escalation section showing the current
  level/target and an inline escalate form (same pattern as the assignment and
  status forms) gated on `tickets.escalate`, with a required reason, a target
  type select (Agent/Department) and a target select populated from the staff
  users / departments services. 409 → stale-version message; 422 → its own
  messages. Reloads on success.
- `ticket-translations.ts` — en/ar keys for the escalation column, section,
  action, fields and messages.

## Tests

- `TicketEndpointsAuthorizationTests` — anonymous `POST /{id}/escalate` → 401.
- `TicketManagementTests` — escalation sets level 1 + target + timestamp,
  writes a history row, bumps the version, writes the outbox row and audits;
  a second escalation reaches level 2 and preserves the first history row;
  blank reason rejected; inactive/unknown agent target rejected and ticket
  unchanged; inactive department target rejected; a `Closed` ticket is not
  escalatable; stale version rejected; unknown ticket → 404 failure; escalation
  leaves `Status` untouched.
- `ticket-detail.spec.ts` — action hidden without permission; submit posts
  target and reason; 409 surfaces the stale-version message.

## Verification

- `dotnet build`; `dotnet test` for `SquadCrm.Api.Tests`,
  `SquadCrm.Persistence.IntegrationTests`, architecture tests.
- Migration check (persistence changed in two modules).
- Frontend `ng test` for the ticket specs, `ng build`, lint/format.
- Browser smoke: escalate a ticket, Arabic/RTL intact, mobile width not broken.
