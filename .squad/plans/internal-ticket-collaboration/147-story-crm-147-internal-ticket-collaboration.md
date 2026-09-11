# Plan — CRM-147 Internal Ticket Collaboration

Branch: `feat/crm-147-internal-ticket-collaboration`

## Scope

Two new capabilities inside the existing `TicketManagement` module: **internal
notes with mentions** and **ticket watchers**. Handoff is already delivered by
CRM-136's assign endpoint and CRM-138's escalation (see intake); this story
adds no competing ownership path.

## Design decisions

1. **One new permission, `tickets.collaborate`**, for every collaboration
   *write* (add note, add/remove watcher). Reads reuse `tickets.view`, the
   CRM-139 precedent that a caller who may read a ticket may read its
   ticket-scoped sub-resources. Collaboration is a single coherent capability;
   splitting it into four permissions would seed catalog entries with no
   distinct consumer (YAGNI).
2. **Mentions are stored, not parsed.** The client sends explicit
   `mentionedUserIds`; the server validates each against
   `IStaffSubjectReferenceReader` (must exist and be active) and rejects the
   whole note otherwise. Free-text `@name` parsing is named stretch by the
   deadline override, and a parser cannot enforce the BR "a mention cannot
   bypass authorization" — an explicit validated id list can.
3. **Watchers are a flat membership set**, not a subscription-preference
   model. AC says watchers "can receive configured ticket updates"; the
   durable delivery of those updates is CRM-155/156 (notifications), which
   consume the outbox. This story produces the membership and the events; it
   builds no dispatcher, because none exists project-wide yet.
4. **Notes are append-only and internal-only.** No edit, no delete: BR says
   internal collaboration is auditable history. `Visibility` is not a column —
   every note is internal by construction, so there is no flag a bug could
   flip to expose one to a customer.
5. **Events are producer-only**, same as every other event in this module:
   domain event on the entity → `TicketManagementOutboxInterceptor` →
   `OutboxMessage`, committed in the same transaction as the note/watcher row.

## Backend — persistence

New table `ticket_internal_note` (schema `ticket_management`):
`id`, `ticket_id`, `body` (max 4000), `created_by` (max 256), `created_at_utc`.
Index `(ticket_id, created_at_utc)` — matches the three existing history
tables, and is what the timeline arm and the notes list read.

New table `ticket_note_mention`: `id`, `note_id` (FK, cascade), `ticket_id`,
`mentioned_user_id`, `created_at_utc`. Unique `(note_id, mentioned_user_id)` so
a duplicated id in one request cannot produce two mention rows.

New table `ticket_watcher`: `id`, `ticket_id`, `user_id`, `added_by` (max 256),
`added_at_utc`. Unique `(ticket_id, user_id)` — membership is a set;
re-adding is a no-op, not a duplicate row.

New table `ticket_watcher_history` (append-only): `id`, `ticket_id`,
`user_id`, `action` (Added/Removed, string-converted), `changed_by`,
`changed_at_utc`, index `(ticket_id, changed_at_utc)`. Removing a watcher
deletes the membership row but never the history — the timeline must still
show that someone was removed (AC 5, BR auditability).

One migration per module: `AddTicketCollaboration` (TicketManagement),
`AddTicketCollaboratePermission` (RoleManagement, `InsertData` row following
the `AddTicketEscalatePermission` precedent exactly).

## Backend — events

`TicketNoteAddedDomainEvent` → `ticket-management.ticket-note-added.v1`
(carries `TicketId`, `NoteId`, `MentionedUserIds`, `CreatedBy`, `OccurredAtUtc`
— **never the note body**: the outbox is a cross-module/external boundary and
internal discussion must not leak through an event payload, AC 6).

`TicketWatcherChangedDomainEvent` → `ticket-management.ticket-watcher-changed.v1`
(`TicketId`, `UserId`, `Action`, `ChangedBy`, `OccurredAtUtc`).

Both added to the interceptor's `Translate` switch (it throws on an
unregistered type, so this is not optional).

## Backend — service + endpoints

`TicketCollaborationService`:
- `AddNoteAsync(ticketId, request, ct)` → validates ticket exists, trims/
  bounds body, de-duplicates and validates every mentioned id
  (`IStaffSubjectReferenceReader`, must be active), inserts note + mention
  rows, raises the domain event, audits.
- `ListNotesAsync(ticketId, pagination, ct)` → chronological, `AsNoTracking`.
- `AddWatcherAsync` / `RemoveWatcherAsync` → validate user is active; add is
  idempotent (already watching → success, no new row, no event); remove of a
  non-watcher → `WatcherNotFound`.
- `ListWatchersAsync`.

Failure enum `TicketCollaborationFailure { None, TicketNotFound,
IneligibleUser, WatcherNotFound }`, mapped to problem responses with
`tickets.*` codes, same shape as the existing ticket endpoints.

Endpoints under the existing `/api/v1/tickets` group:
- `POST /{id}/notes` — `tickets.collaborate`
- `GET  /{id}/notes` — `tickets.view`
- `GET  /{id}/watchers` — `tickets.view`
- `POST /{id}/watchers` — `tickets.collaborate`
- `DELETE /{id}/watchers/{userId}` — `tickets.collaborate`

## Backend — timeline

Two new arms in `TicketTimelineService`, both
`TicketTimelineVisibility.Internal`:
- notes → `TicketNoteAdded`, summary `"Internal note added"` **plus mention
  count only** — the body is never copied into a timeline summary, which the
  customer projection shares code with.
- `ticket_watcher_history` → `TicketWatcherAdded` / `TicketWatcherRemoved`.

The existing audience filter drops both for `Customer`, which is what
satisfies AC 6 for the future portal (CRM-175) — it calls the same service.

## Frontend

`tickets.service.ts`: `TicketInternalNote`, `TicketWatcher`, `AddNoteRequest`,
`AddWatcherRequest` types and five methods mirroring the endpoints.

`ticket-detail`: a PrimeNG collaboration panel beside the existing history —
notes list (author, timestamp, body, mention chips) with an add-note form
(`p-textarea` + `p-multiselect` of active staff for mentions), and a watchers
list with add/remove. Arabic/English strings go in `ticket-translations.ts`
next to the existing ticket keys; no new translation mechanism.

Permission-gated in the UI for UX only (`tickets.collaborate`); the backend
authorizes independently.

## Verification

- `SquadCrm.UnitTests` / `SquadCrm.Api.Tests`: note creation with valid and
  invalid mentions, note body bounds, watcher add idempotency, watcher remove,
  404 on unknown ticket, and the customer-audience timeline assertion that no
  note or watcher entry appears.
- `SquadCrm.ArchitectureTests` + `SquadCrm.Persistence.IntegrationTests`
  (migration check — this story changes persistence).
- Frontend unit tests + build/lint.
