# CRM-144 — Agent Task Reminders

## Story
As a support agent, I want reminders for support tasks so that I am alerted
when follow-up work becomes due.

## Acceptance Criteria
- Authorized task owner/manager can set, update or clear a reminder for an
  eligible open task.
- Reminder is displayed using the user's timezone while persisted/scheduled
  consistently in UTC.
- Due reminder generates a durable reminder event for in-app/external
  notification delivery when those channels are enabled.
- Completed/canceled/ineligible tasks do not generate future reminders.
- Rescheduling a reminder cancels/supersedes the previous scheduled
  occurrence without duplicate alerts.
- Background processing retries are idempotent.

## Business Rules
- Hangfire is the scheduling/execution mechanism, but task/reminder business
  state remains in the application/domain model.
- A reminder cannot grant access to its linked task/ticket/customer;
  notification deep links re-check authorization.
- Reminder delivery failure does not change task completion/state.
- Reminder event identity must be stable enough to prevent duplicate
  notifications on retries.

## Scope note (deadline override, per Linear description)
One optional reminder timestamp per open task, using the already-completed
CRM-198 outbox and CRM-199 Hangfire foundations in the simplest way.
Recurrence, snooze policies, external-channel delivery and sophisticated
timezone scheduling are explicitly out of scope (stretch, non-blocking).

## Reconciliation (2026-09-12)
- Blockers CRM-143, CRM-198, CRM-199 are all Done and merged to `main`.
- No prior CRM-144 branch, plan or `reminder` code exists anywhere in the
  repository — clean slate.
- `AgentTaskManagement` (CRM-143) already provides the entity, outbox
  interceptor, producer-only integration events, endpoints, permissions and
  the Angular task detail screen this story extends.
- CRM-155 (In-App Alerts & Notifications Center) and CRM-156 (Generate
  Ticket, SLA & Task Notifications) are blocked BY this story: this story
  produces the durable reminder event only, it does not build a notification
  centre, delivery channel or consumer.
