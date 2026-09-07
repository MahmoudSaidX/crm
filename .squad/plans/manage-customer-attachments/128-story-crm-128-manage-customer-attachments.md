# Plan — CRM-128 Manage Customer Attachments

Branch: `feat/crm-128-manage-customer-attachments`

New sub-resource under the existing `CustomerManagement` module (same schema
`customer_management`, same `CustomerManagementDbContext`), mirroring
`CustomerNote` (CRM-127) for the child-entity shape and `BrandingService`
(CRM-120/200) for the `IFileStorage` usage pattern.

## Backend — `CustomerManagement` module additions

- `Persistence/CustomerAttachment.cs`
  - `Id`, `CustomerId` (FK to `Customer.Id`), `StorageKey`,
    `OriginalFileName`, `ContentType`, `SizeBytes`, `Description?`,
    `UploadedBy` (current user handle), `UploadedAtUtc`, `RemovedAtUtc?`,
    `RemovedBy?`.
- `CustomerManagementDbContext`: add `DbSet<CustomerAttachment>`; configure
  `ToTable("customer_attachment")`, snake_case columns, FK to `Customer` via
  `HasOne<Customer>().WithMany().HasForeignKey(...).OnDelete(Cascade)` (no
  navigations — same as `CustomerNote`), index
  `(CustomerId, UploadedAtUtc)`.
- EF migration `AddCustomerAttachments`. Strip UTF-8 BOM from generated
  migration files before commit (known CI formatting issue).
- `CustomerContracts.cs` additions:
  - `CustomerAttachmentResponse(Guid Id, Guid CustomerId, string OriginalFileName, string ContentType, long SizeBytes, string? Description, string UploadedBy, DateTimeOffset UploadedAtUtc)`
- `CustomerAttachmentService.cs` (new; constructor DI of
  `CustomerManagementDbContext`, `IFileStorage`, `ICurrentUserAccessor`,
  `IAuditRecorder`):
  - `UploadAsync(customerId, FileUpload, description)`: customer must exist;
    `fileStorage.UploadAsync` (which runs the configured size/type
    validation); persist metadata row; audit `"attachment_added"`.
  - `ListAsync(customerId)`: `AsNoTracking`, `RemovedAtUtc == null`, ordered
    by `UploadedAtUtc` ascending.
  - `OpenAsync(customerId, attachmentId)`: returns stream + content type +
    filename for a non-removed row belonging to that customer; audit
    `"attachment_downloaded"`.
  - `RemoveAsync(customerId, attachmentId)`: soft removal — set
    `RemovedAtUtc`/`RemovedBy`, keep the row and the stored bytes for audit
    retention; audit `"attachment_removed"`.
  - Failure enum: `None, CustomerNotFound, AttachmentNotFound`.
- Project reference: add
  `SquadCrm.BuildingBlocks.Abstractions` only if `IFileStorage` is not
  already reachable through `SquadCrm.BuildingBlocks`.
- `CustomerManagementModule.cs` — endpoints under
  `/api/v1/customers/{customerId:guid}/attachments`:
  - `POST ""` (multipart `IFormFile file` + optional `description`),
    `customers.manage`, `.DisableAntiforgery()`; `FileValidationException`
    → 422 `customers.attachments.invalid_file`; empty file → 400
    `customers.attachments.file_required`.
  - `GET ""` → list, `customers.view`.
  - `GET "/{attachmentId:guid}"` → `Results.Stream` with content type and
    download filename, `customers.view`.
  - `DELETE "/{attachmentId:guid}"` → 204, `customers.manage`.
  - Reuse `NotFoundProblem()` for `CustomerNotFound`; new
    `AttachmentNotFoundProblem()` (`customers.attachments.not_found`).
- No `Permissions.cs` change — reuse `customers.manage`/`customers.view`.

## Frontend — extend `customers` feature

- `customers.service.ts`: `CustomerAttachment` type plus
  `listAttachments`/`uploadAttachment` (FormData)/`removeAttachment` and a
  `attachmentDownloadUrl(customerId, attachmentId)` helper.
- `customer-detail.ts/html`: "Attachments" section after notes —
  `attachments`/`attachmentsLoading`/`attachmentErrorKey` signals, PrimeNG
  `p-fileupload` (custom mode, single file) plus an optional description
  input, `p-table` listing filename/size/uploader/date with a download link
  and a remove action gated by `authorization.has('customers.manage')`,
  empty-state message.
- `customer-translations.ts`: `customers.attachments.*` keys (title, add,
  cancel, empty, fields.*, errors.invalidFile/fileRequired) in English +
  Arabic.

## Tests

- Backend: `CustomerEndpointsAuthorizationTests` — anonymous upload/list/
  download/remove rejected.
- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerAttachmentManagementTests.cs`
  — upload persists metadata + audit, list excludes removed rows and is
  chronological, download of a removed/foreign attachment returns
  `AttachmentNotFound`, oversized/disallowed content type is rejected,
  upload against a nonexistent customer returns `CustomerNotFound`.
- Frontend: extend `customer-detail.spec.ts` for the attachments section.

## Verification

- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests).
- EF migration applied cleanly against local Postgres.
- Frontend `ng test` for updated specs; lint + `format:check`.
- `dotnet format --verify-no-changes`.
