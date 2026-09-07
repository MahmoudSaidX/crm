# CRM-127 — Manage Customer Notes

## Story
As a support user, I want to add internal notes to a customer so that useful
support context is retained with the profile.

## Acceptance Criteria
- Authorized user can add and view customer notes chronologically.
- Notes show author and timestamp.
- Notes are included in internal customer history where applicable.
- Unauthorized/customer-facing contexts cannot access internal notes.

## Business Rules
- Customer notes are internal by default and never exposed to the customer
  portal/chatbot.
- Created notes are immutable; corrections use a new note or explicitly
  audited edit policy.
- Notes belong to exactly one customer.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| Id | UUID | System | Unique, immutable |
| CustomerId | UUID | System | FK to Customer, immutable |
| Body | text | Yes | Required, max length validated |
| AuthorUserId | UUID | System | Populated from current user, immutable |
| CreatedAtUtc | timestamp | System | Immutable |

## Scope note
Deadline override: add/list internal notes with author and timestamp,
protected from customer-facing access. Out of scope for this story:
editing/versioning, mentions, rich text, moderation, and separate
collaboration infrastructure (CRM-147).

- "Included in internal customer history where applicable" is satisfied by
  exposing notes via the existing customer detail read path (list endpoint)
  — CRM-129 (Customer Interaction History Timeline) builds the cross-module
  timeline read model later and depends on this story; no timeline
  integration is built here.
- "Never exposed to the customer portal/chatbot" is satisfied structurally:
  notes live only in `CustomerManagement` (internal module), no
  customer-portal/chatbot module references this entity or endpoint.
- Add gated by existing `customers.manage`; list gated by existing
  `customers.view` (same permission split as CRM-122/124/126).
- New sub-resource under the existing `CustomerManagement` module (same
  schema, same DbContext) — no new module.
- Notes are immutable per Business Rules — no update/delete endpoint.
