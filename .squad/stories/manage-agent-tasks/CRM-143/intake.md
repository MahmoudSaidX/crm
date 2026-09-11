# CRM-143 — Manage Agent Tasks

## Story
As a support agent, I want to create and manage support tasks so that
follow-up actions are tracked and not forgotten.

## Acceptance Criteria
- Authorized agent can create, view, edit and complete tasks they own or
  are permitted to manage.
- Task can optionally link to an accessible ticket and/or customer.
- Task has title, optional details, owner, due date/time, status and
  timestamps.
- 'My Tasks' view supports useful filtering/sorting for open, due and
  completed tasks.
- Completing a task preserves history rather than deleting it.
- Task changes emit durable events needed by reminders/notifications where
  applicable.

## Business Rules
- Task ownership is explicit; linking a task to a ticket/customer does not
  grant access to that resource.
- Linked resource access is validated at creation/read/navigation time.
- Completed tasks are immutable in completion metadata except through an
  explicitly supported reopen workflow.
- Task due/reminder timestamps are stored in UTC with user-facing timezone
  conversion.
- Deleting historical completed tasks is not the normal lifecycle.

## Scope note (deadline override, per Linear description)
Simple personal/support tasks only: title, optional
ticket/customer link, due date, Open/Completed status, My Tasks view.
Delegation workflows, recurring tasks, task dependencies, complex manager
permissions and a task engine are explicitly out of scope (stretch,
non-blocking) — do not build them.

- New backend module `AgentTaskManagement` (schema `agent_task_management`),
  following the `TicketManagement` module shape: minimal-API module,
  `Permissions.cs` (`tasks.view`, `tasks.create`, `tasks.edit`,
  `tasks.complete`), `AgentTaskService.cs`, EF Core entity `AgentTask`,
  outbox interceptor + integration events per CRM-198 pattern.
- Ownership/"My Tasks" filtering is ad hoc in the service layer (resolve
  caller id from `ICurrentUserAccessor`, filter `OwnerUserId ==
  currentUserId`) — same pattern `TicketService.ListAsync` already uses for
  `assignedToMe`. No generic ownership/scope primitive exists yet
  (`ICurrentUserAccessor` doc comment confirms this), so this story does not
  invent one.
- Ticket/customer link validation reuses existing lookup-contract pattern:
  `ICustomerExistsLookup` (CustomerManagement.Contracts) for customer links;
  add an equivalent `ITicketAccessibleLookup`-style existence check against
  TicketManagement.Contracts for ticket links (existence only — this story
  does not grant ticket access via a task link, per Business Rules).
- "Emit durable events needed by reminders/notifications" = raise domain
  events (`AgentTaskCreated/Completed/Reopened`) and translate to outbox
  integration events, same producer-only pattern as TicketManagement today
  — no consumer/dispatcher exists yet project-wide, so this story does not
  build one (CRM-144 Agent Task Reminders is the future consumer).
- Reopen is explicitly supported per Business Rules, so Status is
  Open/Completed with a reopen action, not a one-way transition.
