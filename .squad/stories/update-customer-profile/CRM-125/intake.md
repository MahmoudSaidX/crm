# CRM-125 — Update Customer Profile

## Story
As a support user, I want to update customer profile information so that
customer records stay accurate.

## Acceptance Criteria
- Authorized user can edit supported mutable profile fields.
- Validation and optimistic concurrency prevent invalid/lost updates.
- Changes are visible immediately after successful save.
- Material profile changes are audited.

## Business Rules
- CustomerNumber cannot be changed.
- Updates cannot assign inactive/unauthorized organizational references.
- Contact details are managed through CRM-126 rather than duplicated here.
- Conflicting concurrent edits return a clear conflict response.

## Fields
Editable: FirstName, LastName, PreferredLanguage, DepartmentId, BranchId,
Status (subject to `customers.manage`, same permission as create).
CustomerNumber is read-only.

## Scope note
Deadline override: ordinary validated customer-profile editing, audited via
the existing audit capability. Optimistic concurrency UX/version-conflict
handling beyond a clear conflict response, and generalized patch/change-
tracking infrastructure, are stretch/non-blocking.

- `Customer.Status` currently only has `Active` (CRM-122 predates any
  deactivation concept). Making Status genuinely editable per the fields
  dictionary requires adding `CustomerStatus.Inactive` — a minimal enum
  extension, not a new subsystem.
- Optimistic concurrency: no existing precedent in this codebase. Uses
  Postgres's native `xmin` system column as the EF Core concurrency token
  (`UseXminAsConcurrencyToken()`) — no new physical column/migration needed.
  Client round-trips an opaque `version` value from the read response.
- Edit gated by existing `customers.manage`; matches CRM-122/123/124/126
  permission split (view vs manage).
- Same module (`CustomerManagement`), same `CustomerService`/
  `CustomerManagementModule` — no new module.
