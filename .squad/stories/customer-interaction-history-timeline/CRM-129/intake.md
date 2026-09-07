# CRM-129 — Customer Interaction History Timeline

## Story
As a support user, I want a customer interaction timeline so that I
understand prior support activity before responding.

## Acceptance Criteria
- Customer profile exposes a chronological interaction timeline.
- Timeline includes customer/profile events available today (created,
  updated, contact changes, notes, attachments); ticket and communication
  events are added by those modules when they exist.
- Entries show event type, timestamp and safe summary/context.
- Access follows customer scope and event-specific visibility.

## Business Rules
- Internal-only entries (notes) are never exposed to customer-facing
  consumers.
- Entries are ordered deterministically by occurrence time.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| EventType | string | System | e.g. CustomerCreated, ContactUpdated, NoteAdded, AttachmentAdded |
| OccurredAtUtc | timestamp | System | From underlying record's own timestamp |
| ActorDisplay | string, nullable | System | Author/actor where known (e.g. note author) |
| RelatedEntityType | string | System | e.g. Customer, CustomerNote, CustomerAttachment |
| RelatedEntityId | UUID | System | Id of underlying record |
| Summary | string | System | Safe, non-sensitive summary |
| Visibility | enum (Internal/Customer) | System | Internal for notes; Customer for profile/contact/attachment events |

## Scope note
Deadline override in the Linear description explicitly permits a
query-composed timeline for the demo, calling a generalized event
projection/read model, snapshotting, cross-module normalization, and
advanced pagination "stretch/non-blocking". Decision confirmed with user
2026-09-07: build query-composed now, not an event-sourced read model.

- No domain/integration events or outbox exist yet anywhere in the real
  modules (only the `ArchitectureFixture` sample module has that
  machinery); Ticket and Communication modules don't exist yet. Building a
  full event read-model now for a single producer module would be
  speculative infrastructure ahead of any second producer — deferred until
  a module actually needs to publish into the timeline.
- Implementation composes the timeline by querying existing
  `CustomerManagement` tables directly (customer, contact, notes,
  attachments) — this stays within the module's own schema, so it does not
  violate the "no direct access to another module's private tables"
  boundary.
- Gated by existing `customers.view` (internal viewer sees notes;
  visibility field lets a future customer-facing consumer filter them out
  — no such consumer exists yet, so filtering is enforced defensively in
  the response builder).
- New read-only sub-resource under the existing `CustomerManagement`
  module — no new module, no new persisted table (entries are computed
  from existing rows, not stored).
- Out of scope: ticket/communication event sources (modules don't exist),
  outbox/projection infrastructure, pagination beyond a simple ordered
  list, snapshotting.
