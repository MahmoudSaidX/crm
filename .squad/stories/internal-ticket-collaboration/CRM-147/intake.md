# CRM-147 — Internal Ticket Collaboration — Notes, Mentions, Watchers & Handoff

## Story
As a support team member, I want internal notes, mentions, watchers and
controlled handoff on tickets so that teams can collaborate without exposing
internal discussion to customers.

## Acceptance Criteria
- Authorized users can add chronological internal notes with author/timestamp.
- Users can @mention eligible teammates; mentions generate durable
  notification events.
- Authorized users can add/remove eligible watchers/followers and watchers can
  receive configured ticket updates.
- Authorized user can hand off/reassign a ticket to an eligible
  agent/department with a reason.
- Collaboration/handoff events appear in internal ticket history.
- Customer-facing APIs/portal/conversations never expose internal notes,
  watcher lists or internal handoff details.

## Business Rules
- Internal collaboration is separate from customer-visible Conversation
  messages.
- Mentioned/watching users must have organizational access to the ticket; a
  mention cannot bypass authorization.
- Handoff uses the canonical ticket assignment/reassignment capability; it must
  not create a competing ownership model.
- Handoff reason is required and previous ownership remains in history.
- No standalone Slack-like internal chat is introduced in this scope.

## Scope note (deadline override, per Linear description)
Internal ticket notes plus lightweight mentions and watchers. Full watcher
lifecycle (per-event subscription preferences), a handoff *workflow* engine,
mention parsing/notification orchestration and collaboration-platform behavior
are explicitly stretch/non-blocking — do not build them.

## Reconciliation findings
- Blockers CRM-198, CRM-111, CRM-139, CRM-135 are all Done and merged.
- No `feat/crm-147-*` branch, no prior plan, no note/watcher table, service or
  endpoint in the tree. Clean start.
- **Handoff already exists.** `POST /api/v1/tickets/{id}/assign` (CRM-136) is
  the canonical reassignment capability: it validates the target agent via
  `IStaffSubjectReferenceReader`, *requires* a reason when replacing an
  existing owner, writes an append-only `ticket_assignment_history` row, and
  already surfaces as an `Internal`-visibility timeline entry (CRM-139). The
  Business Rule forbids a competing ownership model, so this story adds **no
  handoff code** — AC 4 and the handoff half of AC 5 are already satisfied.
  Department handoff is expressed by the existing escalation capability
  (CRM-138, `TicketEscalationTargetType.Department`).
- `TicketTimelineService` was written anticipating this story: its type-level
  remarks name CRM-147 internal notes as a future projection arm.
- `TicketManagementOutboxInterceptor` drains domain events from *any* tracked
  `HasDomainEvents` entity, so a note entity can raise a mention event without
  touching the `Ticket` aggregate.
