# CRM-128 — Manage Customer Attachments

## Story
As a support user, I want to attach files to a customer profile so that
relevant supporting information can be retained securely.

## Acceptance Criteria
- Authorized user can upload, list and download supported customer
  attachments.
- File size/type rules are validated before acceptance.
- File bytes use the shared storage abstraction and metadata is linked to the
  customer.
- Deletion/removal follows permission and retention policy.

## Business Rules
- File authorization is checked through customer access permissions.
- Original filename is display metadata, never a trusted storage path.
- Unsupported or unsafe files are rejected.
- Historical/audit requirements may prevent physical deletion.

## Fields
| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| Id | UUID | System | Unique, immutable |
| CustomerId | UUID | System | FK to Customer, immutable |
| StorageKey | string | System | Provider-neutral key from IFileStorage |
| OriginalFileName | string | System | Display metadata only |
| ContentType | string | System | Allow-listed by FileStorage options |
| SizeBytes | long | System | Bounded by FileStorage MaxSizeBytes |
| Description | string | No | Optional, max 500 |
| UploadedBy | string | System | Current user handle |
| UploadedAtUtc | timestamp | System | Immutable |
| RemovedAtUtc / RemovedBy | timestamp / string | System | Set on soft removal |

## Scope note
Deadline override: upload/list/download with size/type validation and
authorization, reusing the completed CRM-200 file-storage foundation. Out of
scope: retention workflows, malware scanning, versioning, new storage
infrastructure.

- Size/type validation is already owned by `ConfiguredFileUploadValidator`
  inside `IFileStorage.UploadAsync`; this story surfaces
  `FileValidationException` as a 422 problem, it does not add a second
  validation catalog.
- "Deletion follows permission and retention policy" is implemented as a
  soft removal (`RemovedAtUtc`/`RemovedBy`) gated by `customers.manage`, with
  the audit record retained and file bytes left in storage — the BR
  explicitly allows historical requirements to prevent physical deletion.
  Precedent for physical delete (Branding) does not apply: branding assets
  have no audit-retention rule.
- The `FileReferenceId` field from the Linear dictionary is not persisted:
  `FileReference.Id` is generated per upload and never resolvable back
  through `IFileStorage` (the same reason `BrandingAsset` stores only
  `StorageKey`). The attachment row's own `Id` is the stable identifier.
- Upload/remove gated by `customers.manage`; list/download by
  `customers.view` — same split as CRM-122/124/126/127.
- New sub-resource under the existing `CustomerManagement` module (same
  schema, same DbContext) — no new module.
