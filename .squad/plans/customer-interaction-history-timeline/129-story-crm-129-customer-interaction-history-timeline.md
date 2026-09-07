# Plan — CRM-129 Customer Interaction History Timeline

Branch: `feat/crm-129-customer-interaction-history-timeline`

Read-only sub-resource under the existing `CustomerManagement` module (same
schema `customer_management`, same `CustomerManagementDbContext`) — no new
module, no new table. Timeline entries are computed at query time from
existing rows (`Customer`, `CustomerContact`, `CustomerNote`,
`CustomerAttachment`), per the query-composed decision in the intake.

## Backend — `CustomerManagement` module additions

- `CustomerContracts.cs`:
  - `enum CustomerTimelineVisibility { Internal, Customer }`
  - `sealed record CustomerTimelineEntryResponse(string EventType, DateTimeOffset OccurredAtUtc, string? ActorDisplay, string RelatedEntityType, Guid RelatedEntityId, string Summary, CustomerTimelineVisibility Visibility)`
- `CustomerTimelineService.cs` (new, mirrors `CustomerNoteService`/
  `CustomerAttachmentService` conventions — constructor DI of
  `CustomerManagementDbContext`):
  - `GetAsync(customerId)`: validate customer exists (reuse
    `CustomerNotFound` failure enum pattern); `AsNoTracking` query each of
    the four tables scoped to `customerId`, project each row to a
    `CustomerTimelineEntryResponse`:
    - Customer → `EventType: "CustomerCreated"`, `OccurredAtUtc: CreatedAtUtc`,
      `ActorDisplay: null`, `RelatedEntityType: "Customer"`,
      `Summary: "Customer profile created"`, `Visibility: Customer`. If
      `UpdatedAtUtc != CreatedAtUtc`, also emit a second `"CustomerUpdated"`
      entry at `UpdatedAtUtc`.
    - CustomerContact → one `"ContactAdded"` entry per row at
      `CreatedAtUtc`, `Summary` from `Type`/`Label` (no raw contact value,
      to keep summaries safe), `Visibility: Customer`.
    - CustomerNote → one `"NoteAdded"` entry per row at `CreatedAtUtc`,
      `ActorDisplay` resolved from `AuthorUserId` (best-effort display name
      via existing user lookup used elsewhere, or the raw id if
      unavailable — no new cross-module call), `Summary` a truncated
      excerpt of `Body`, `Visibility: Internal`.
    - CustomerAttachment → one `"AttachmentAdded"` entry per row at
      `UploadedAtUtc` (`ActorDisplay: UploadedBy`, `Summary:
      OriginalFileName`, `Visibility: Customer`); skip rows where
      `RemovedAtUtc` is set.
  - Merge-sort all entries by `OccurredAtUtc` ascending, return as a single
    ordered list — deterministic ordering satisfies the intake's business
    rule without needing a stored sequence.
  - Failure enum: `None, CustomerNotFound`.
- `CustomerManagementModule.cs` — new endpoint:
  - `customers.MapGet("/{customerId:guid}/timeline", GetTimelineAsync).RequireAuthorization(PermissionPolicies.CustomersView)`.
  - `GetTimelineAsync` calls `CustomerTimelineService.GetAsync`, reuses the
    existing `NotFoundProblem()` helper for `CustomerNotFound`.
  - Internal-only (`Visibility: Internal`) entries are still returned here —
    this endpoint is the internal agent view, gated by `customers.view`
    like notes today. No customer-portal/chatbot consumer exists yet; when
    one is built it must call a filtered variant, not this endpoint —
    noted as a follow-up, not built now.
- No `Permissions.cs` change — reuse `customers.view`.
- No migration — no new table.

## Frontend — extend `customers` feature

- `customers.service.ts`: add `CustomerTimelineEntry` type and
  `getTimeline(customerId)` method mirroring `listNotes`.
- `customer-detail.ts/html`: add a "Timeline" section — `timeline`/
  `timelineLoading` signals, `loadTimeline()` called alongside
  `loadNotes()`/`loadAttachments()` in `load()`. Simple chronological list
  (event type + summary + timestamp + actor when present), no pagination
  controls (list is short today), empty state when no entries. Visible to
  anyone with `customers.view` (no manage-gated actions — read-only
  section).
- `customer-translations.ts`: add `customers.timeline.*` keys (title,
  empty, eventTypes.*) in English + Arabic.

## Tests

- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerTimelineManagementTests.cs`
  covering: timeline includes profile/contact/note/attachment entries in
  chronological order, removed attachments excluded, internal notes
  present (this is the internal endpoint), request against nonexistent
  customer returns `CustomerNotFound`.
- Backend: extend `SquadCrm.Api.Tests/CustomerEndpointsAuthorizationTests.cs`
  with `GetTimeline_RejectsAnonymousRequest`.
- Frontend: extend `customer-detail.spec.ts` for the timeline section
  (renders ordered entries, empty state).

## Verification

- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests).
- Frontend `ng test` for updated specs; `ng build`/lint.
- Browser smoke: view timeline in English and Arabic (RTL), confirm
  ordering and that notes/attachments show up alongside profile events.
