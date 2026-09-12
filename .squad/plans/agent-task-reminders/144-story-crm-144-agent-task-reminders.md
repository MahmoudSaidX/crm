# Plan — CRM-144 Agent Task Reminders

Branch: `feat/crm-144-agent-task-reminders`

Extends the existing `SquadCrm.Modules.AgentTaskManagement` module (CRM-143)
and consumes the CRM-198 outbox + CRM-199 Hangfire foundations. No new
module, no new architectural pattern, no notification module (that is
CRM-155/CRM-156, which this story blocks).

## Design decisions

**Reminder state lives on `AgentTask`, not in a separate aggregate.** The
scope override allows exactly one optional reminder per task, so a
one-to-one child table would be pure overhead.

**Idempotency/supersession via `ReminderEventId`.** Setting or rescheduling a
reminder mints a fresh `ReminderEventId` (Guid). When the reminder fires, the
outbox row's primary key IS that `ReminderEventId` (the interceptor already
uses `integrationEvent.EventId` as `OutboxMessage.Id`). Consequences:
- A retried sweep cannot insert a second outbox row for the same occurrence —
  the primary key rejects it, and the `Scheduled -> Triggered` transition is
  written in the SAME `SaveChanges` transaction as the outbox row, so either
  both happen or neither does (AC "retries are idempotent").
- Rescheduling replaces the id, so a superseded occurrence can never be
  confused with the new one (AC "without duplicate alerts").

**Hangfire is execution only.** The recurring job is a thin entry point that
calls `AgentTaskReminderService.TriggerDueRemindersAsync`; all reminder state
(`ReminderStatus`, `ReminderAtUtc`, `ReminderEventId`,
`ReminderTriggeredAtUtc`) is domain state on the entity, never Hangfire job
state (CLAUDE.md + BR). A sweep, not a per-reminder scheduled job: a
per-reminder job id would put business identity into Hangfire, and a
cancelled/rescheduled reminder would require deleting Hangfire jobs to stay
correct.

**Reuses `tasks.edit`.** Setting a reminder is an edit of a task the caller
owns. Adding a `tasks.reminder` permission would require a RoleManagement
migration and a permission-catalog change for no authorization gain, since
ownership is what actually gates the action.

**Timezone.** Persisted and swept in UTC (`DateTimeOffset`, `timestamptz`);
the Angular screen renders through the existing `DatePipe` (browser
timezone) and posts an ISO instant. No server-side per-user timezone model
exists in the app today and this story does not invent one.

## Backend

`src/backend/src/Modules/AgentTaskManagement/SquadCrm.Modules.AgentTaskManagement/`

- `Persistence/AgentTask.cs`
  - New `AgentTaskReminderStatus` enum: `None`, `Scheduled`, `Triggered`,
    `Cancelled`.
  - New properties: `ReminderAtUtc`, `ReminderStatus`, `ReminderEventId?`,
    `ReminderTriggeredAtUtc?` (all private-set, mutated only by the methods
    below).
  - `SetReminder(DateTimeOffset reminderAtUtc, DateTimeOffset changedAtUtc)`:
    mints a new `ReminderEventId`, `ReminderStatus = Scheduled`, clears
    `ReminderTriggeredAtUtc`, bumps `Version`. Eligibility (open, owner,
    version, future timestamp) enforced in the service before the call.
  - `ClearReminder(changedAtUtc)`: `ReminderAtUtc = null`,
    `ReminderStatus = None`, `ReminderEventId = null`, bumps `Version`.
  - `Complete()`: additionally cancels a `Scheduled` reminder
    (`ReminderStatus = Cancelled`, `ReminderEventId = null`) so a completed
    task cannot generate a future reminder (AC).
  - `Reopen()`: leaves a cancelled reminder cancelled — it is NOT
    resurrected, which would fire an already-past reminder immediately.
  - `TriggerReminder(triggeredAtUtc)`: `Scheduled -> Triggered`, stamps
    `ReminderTriggeredAtUtc`, raises
    `AgentTaskReminderDueDomainEvent(ReminderEventId, ...)`. Does NOT bump
    `Version`: firing is a system event, not a user edit, and must not
    invalidate a version the owner is holding in an open form.
- `Events/AgentTaskReminderDueDomainEvent.cs` +
  `Events/AgentTaskReminderDueIntegrationEvent.cs`
  (`agent-task-management.agent-task-reminder-due.v1`), carrying `EventId`
  (= `ReminderEventId`), `TaskId`, `Title`, `OwnerUserId`, `ReminderAtUtc`,
  `OccurredAtUtc`. Producer only.
- `Persistence/AgentTaskManagementOutboxInterceptor.cs`: new `Translate`
  branch mapping the domain event, passing `ReminderEventId` through as the
  integration event's `EventId` (NOT `Guid.NewGuid()` — that is the whole
  idempotency mechanism).
- `Persistence/AgentTaskManagementDbContext.cs`: map the four new columns,
  `ReminderStatus` as `string` (existing convention), plus a filtered index
  on `(reminder_at_utc)` where `reminder_status = 'Scheduled'` for the sweep.
- `AgentTaskReminderService.cs` (new, internal): `TriggerDueRemindersAsync`
  — loads tasks with `ReminderStatus == Scheduled &&
  ReminderAtUtc <= now && Status == Open` in a bounded batch, calls
  `TriggerReminder`, one `SaveChangesAsync` per batch. Runs outside an HTTP
  request, so it never touches `ICurrentUserAccessor`.
- `BackgroundProcessing/AgentTaskReminderJob.cs` (new, public): thin
  Hangfire entry point, mirrors `ArchitectureFixtureOutboxJob`'s shape.
- `AgentTaskContracts.cs`: `SetAgentTaskReminderRequest(DateTimeOffset
  ReminderAtUtc, int Version)`; `AgentTaskResponse` gains `reminderAtUtc`,
  `reminderStatus`, `reminderTriggeredAtUtc`.
- `AgentTaskManagementModule.cs`:
  - `PUT /api/v1/tasks/{id}/reminder` (set/update) and
    `DELETE /api/v1/tasks/{id}/reminder` (clear), both
    `RequireAuthorization(PermissionPolicies.TasksEdit)`.
  - New failures: `ReminderOnClosedTask` (422 `tasks.reminder_task_closed`),
    `ReminderInPast` (422 `tasks.reminder_in_past`).
  - Registers `AgentTaskReminderService` + `AgentTaskReminderJob`.
- `src/backend/src/Api/SquadCrm.Api/`: register the recurring job
  (`agent-task-reminder-dispatch`, `Cron.Minutely`) alongside the existing
  architecture-fixture registration.

## Frontend

`src/frontend/projects/agent-crm/src/app/tasks/`

- `tasks.service.ts`: `setReminder(id, {reminderAtUtc, version})`,
  `clearReminder(id, version)`.
- `task-create/task-create.service.ts`: `AgentTask` gains the three new
  read fields.
- `task-detail.ts` / `.html`: a Reminder card — PrimeNG `DatePicker`
  (`showTime`, `[minDate]="now"`) plus Save/Clear buttons, gated on
  `tasks.edit` and hidden for a completed task. Shows the scheduled time via
  `DatePipe` (browser timezone) and the `Triggered`/`Cancelled` state as a
  `p-tag`. 409 -> "reload and try again", 403 -> forbidden, 422 -> the
  specific reminder message (mirrors the existing `submitAction` handling).
- `task-translations.ts`: EN + AR strings for every new label/error.

## Tests

- `SquadCrm.Persistence.IntegrationTests/AgentTaskReminderTests.cs`:
  set/update/clear; reschedule replaces `ReminderEventId`; completing
  cancels a scheduled reminder; the sweep triggers exactly one outbox row
  and transitions to `Triggered`; a second sweep produces NO second row
  (idempotency); a completed/cleared task is never swept; a reminder on a
  completed task is rejected.
- `SquadCrm.Api.Tests/AgentTaskEndpointsAuthorizationTests.cs`: the two new
  endpoints reject anonymous and permission-less callers.
- `SquadCrm.Api.Tests/BackgroundProcessingRegistrationTests.cs`: the new
  recurring job is registered.
- `task-detail.spec.ts`: set and clear a reminder, and the stale-version
  error path.

## Out of scope
Recurrence, snooze, external channels (email/SMS/WhatsApp), per-user
timezone preference, a notification centre or any consumer of the reminder
event, reminders on tickets (CRM-150 owns SLA timers).
