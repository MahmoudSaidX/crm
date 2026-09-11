# Plan — CRM-136 Assign & Reassign Tickets to Agents

## Intake

- **Linear story:** CRM-136 "Assign & Reassign Tickets to Agents" (Sprint 3 —
  Ticket Management, epic CRM-130). Blockers CRM-133, CRM-135, CRM-111,
  CRM-118, CRM-119 are all Done and merged.
- **Goal:** one authorized assignment/reassignment action on an existing
  ticket, recorded in ticket history, emitting a durable event, protected by
  the existing optimistic-concurrency token.
- **Linear's Deadline Acceptance Override is binding:** "assignment/
  reassignment to an active agent and record ticket history; a simple optional
  reason is enough. Capability matrices, workload balancing, elaborate
  eligibility policy, stale-assignment recovery and generalized assignment
  engines are stretch/non-blocking."
- **Reconciliation:** no `feat/crm-136-*` branch, no prior plan, no assignment
  endpoint, service method, history table or domain event in the tree.
  `Ticket.Version` and `Ticket.UpdatedAtUtc` were added by CRM-135 explicitly
  for this story to be the first writer. Clean start.

## Scope gaps carried forward (documented, not silently dropped)

- **Branch/Department scope eligibility** — `StaffUser.Branch`/`.Department`
  are free-text strings, not foreign keys to BranchManagement/
  DepartmentManagement, and `ICurrentUserAccessor` still carries no
  organizational-scope model (same gap CRM-123/134/135 recorded). Target
  eligibility is therefore validated on **active staff user** only, which is
  exactly what the Deadline Acceptance Override reduces it to. No scope model
  is invented here.
- **Role/capability eligibility ("is an agent")** — no agent-capability or
  role-requirement model exists; the override rules out a capability matrix.
  Any active staff user is an eligible target.
- **Automated assignment (`AssignmentSource.Automation`)** — CRM-151/152. The
  source is persisted as a column with only `Manual` written today, so the
  automation story reuses the same canonical `TicketService.AssignAsync`
  instead of adding a parallel path (BR "automated and manual assignment use
  the same canonical capability").
- **Notifications / reporting consumers** — CRM-156/188. Producer side only:
  the outbox row is written; no dispatcher/consumer exists yet (same as
  CRM-133's `TicketCreatedIntegrationEvent`).
- **Ticket history timeline UI** — CRM-139. This story creates the assignment
  history rows; it renders no timeline.

## Backend — module `TicketManagement`

- `Persistence/TicketAssignmentHistory.cs` — new entity/table
  `ticket_assignment_history`: `Id`, `TicketId`, `PreviousAgentId?`,
  `NewAgentId`, `Reason?` (max 500), `Source` (enum as string),
  `ChangedBy` (actor handle), `ChangedAtUtc`. Indexed by
  `(TicketId, ChangedAtUtc)` so CRM-139 can read a ticket's history in order.
  Written in the SAME transaction as the ticket update, so a retried/failed
  save cannot produce a duplicate or orphan history row (BR).
- `Persistence/Ticket.cs` — `Assign(Guid targetAgentId, DateTimeOffset atUtc)`
  domain method: sets `AssignedAgentId`, `UpdatedAtUtc`, increments `Version`,
  raises `TicketAssignedDomainEvent`. Only mutation path; `AssignedAgentId`
  setter becomes private.
- `Events/TicketAssignedDomainEvent.cs` + `TicketAssignedIntegrationEvent.cs`
  (contract `ticket-management.ticket-assigned.v1`), plus the matching branch
  in `TicketManagementOutboxInterceptor.Translate` (the switch already throws
  on an unmapped event type).
- `TicketContracts.cs` — `TicketAssignmentSource { Manual, Automation }`,
  `AssignTicketRequest(Guid TargetAgentId, string? Reason, int Version)`,
  new `TicketMutationFailure` values: `TicketNotFound`, `IneligibleAgent`,
  `ReasonRequired`, `StaleVersion`, `AlreadyAssignedToTarget`.
- `TicketService.AssignAsync` — load tracked ticket → 404; `Version` mismatch
  → stale; target already the current owner → no-op failure; target resolved
  through `IStaffSubjectReferenceReader.FindByIdAsync` and must be active →
  ineligible; reason required (non-blank, ≤500) when replacing an existing
  owner; then `ticket.Assign(...)`, add history row, single `SaveChangesAsync`
  (catch `DbUpdateConcurrencyException` → stale), then audit `"assigned"`.
- `TicketManagementModule.cs` — `tickets.MapPost("/{id:guid}/assign", …)
  .ValidatesDataAnnotations<AssignTicketRequest>()
  .RequireAuthorization(PermissionPolicies.TicketsAssign)`; problem responses:
  404 `tickets.not_found`, 409 `tickets.stale_version`, 422
  `tickets.ineligible_agent`, 422 `tickets.reason_required`.
- New permission `tickets.assign` — declared in this module's `Permissions.cs`,
  added to RoleManagement's catalog + policy registration, seeded by a new
  RoleManagement migration `AddTicketAssignPermission` (mirrors
  `AddTicketPermissions`).
- `SquadCrm.Modules.TicketManagement.csproj` — project reference to
  `SquadCrm.Modules.StaffIdentity.Contracts` (contracts project only; module
  boundary intact).

## Frontend — feature `tickets`

- `tickets.service.ts` — `assign(id, request)` POST
  `/api/v1/tickets/{id}/assign`.
- `ticket-detail.ts/html/scss` — "Assign"/"Reassign" button, shown only when
  the caller has `tickets.assign`, revealing an inline form (the pattern
  `customer-detail` already uses for in-place edits — no `p-dialog` exists
  anywhere in this app yet) with a PrimeNG `p-select` of active staff users
  (existing staff-users service, needs `users.view`; the form says so when the
  list cannot be loaded) and a reason `textarea` that is required when the
  ticket already has an owner.
  On success the detail reloads (new `version`/`updatedAtUtc`); a 409 shows a
  "changed by someone else, reload" message rather than retrying blindly.
- `ticket-translations.ts` — en/ar keys for the new labels/messages.

## Tests

- `TicketEndpointsAuthorizationTests` — anonymous POST
  `/api/v1/tickets/{id}/assign` returns 401.
- `TicketManagementTests` — assign unassigned ticket writes owner, history row,
  bumped version and outbox row; reassign without a reason fails; reassign with
  a reason succeeds and records previous owner; inactive/unknown target agent
  fails and leaves the ticket unchanged; stale version fails and leaves the
  ticket unchanged.
- `ticket-detail.spec.ts` — assign action hidden without permission; dialog
  submits the selected agent + reason; 409 surfaces the stale-version message.

## Verification

- `dotnet build`; `dotnet test` for `SquadCrm.Api.Tests`,
  `SquadCrm.Persistence.IntegrationTests` and the architecture tests
  (module-boundary change).
- `dotnet ef migrations` check — persistence changed in two modules.
- Frontend `ng test` for the ticket specs, `ng build`, lint/format.
- Browser smoke: assign and reassign from the detail screen, Arabic/RTL intact,
  mobile width not broken.
