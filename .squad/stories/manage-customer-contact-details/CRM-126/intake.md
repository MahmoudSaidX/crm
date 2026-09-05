# CRM-126 — Manage Customer Contact Details

## Story
As a support user, I want to manage a customer's contact details so that
agents can communicate through the correct channels.

## Acceptance Criteria
- Authorized user can add, edit and deactivate supported email/phone contacts.
- Contact values are normalized and validated by type.
- A primary contact can be designated per contact type.
- Changes are audited and immediately available to communication workflows.

## Business Rules
- At most one active primary contact exists per type per customer.
- Removing the primary contact requires selecting another primary when active
  contacts of that type remain.
- Historical communication keeps the original destination snapshot even if
  contact details later change.
- Duplicate contact policy is configurable and must not silently merge
  customers.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| ContactId | UUID | System | Unique, immutable |
| CustomerId | UUID | System | FK to Customer, immutable |
| Type | enum (Email/Phone) | Yes | Immutable after creation |
| Value | string | Yes | Trimmed, validated by Type |
| NormalizedValue | string | System | Lowercased email / digits-only phone |
| Label | string | No | Optional free text |
| IsPrimary | boolean | No | Default false |
| IsActive | boolean | System | Active by default; false once deactivated |
| VerifiedAtUtc | timestamp | No | Not set by this story (no verification workflow yet) |
| CreatedAtUtc / UpdatedAtUtc | timestamp | System | |

## Scope note
Deadline override: simple email/phone contact CRUD with basic validation and
an optional primary flag. Out of scope for this story: sophisticated
normalization/deduplication policy, verification workflows, generalized
contact-type engines, communication-readiness orchestration.

- "Historical communication keeps the original destination snapshot" has no
  effect yet — no communication-sending feature exists (CRM-165/166/168 are
  still Backlog). Nothing to build; contact edits simply update the current
  record, satisfied trivially until a communication module exists.
- "Duplicate contact policy is configurable" refers to the CRM-122-level
  customer dedup policy, not per-contact duplicate values. No new
  cross-customer merge logic is introduced here — contacts never cause
  customer records to merge, satisfying the rule with no new code.
- Add/edit/deactivate gated by existing `customers.manage`; list gated by
  existing `customers.view` (same permission split as CRM-122/123/124).
- New sub-resource under the existing `CustomerManagement` module (same
  schema, same DbContext) — no new module.
