# Plan — CRM-127 Manage Customer Notes

Branch: `feat/crm-127-manage-customer-notes`

New sub-resource under the existing `CustomerManagement` module (same schema
`customer_management`, same `CustomerManagementDbContext`) — no new module,
mirroring how `CustomerContact`/`CustomerContactService` (CRM-126) already
handle a child entity of `Customer`.

## Backend — `CustomerManagement` module additions

- `Persistence/CustomerNote.cs`
  - `Id`, `CustomerId` (FK to `Customer.Id`), `Body` (string, required, max
    4000), `AuthorUserId` (Guid, from current user), `CreatedAtUtc`. No
    `UpdatedAtUtc` — notes are immutable, no update path.
- `CustomerManagementDbContext`: add `DbSet<CustomerNote> CustomerNotes`;
  configure `ToTable("customer_note")`, FK to `Customer` via
  `HasOne<Customer>().WithMany().HasForeignKey(...).OnDelete(Cascade)` (no
  navigation properties either direction — mirrors `CustomerContact`
  configuration exactly).
- EF migration `AddCustomerNotes`. Strip UTF-8 BOM from the generated
  migration files before commit (known CI formatting issue, see commit
  ac03a11).
- `CustomerContracts.cs` additions:
  - `AddCustomerNoteRequest([Required, MaxLength(4000)] string Body)`
  - `CustomerNoteResponse(Guid Id, Guid CustomerId, string Body, Guid AuthorUserId, DateTimeOffset CreatedAtUtc)`
- `CustomerNoteService.cs` (new, mirrors `CustomerContactService`
  conventions — constructor DI of `CustomerManagementDbContext`,
  `ICurrentUserAccessor`, `IAuditRecorder`):
  - `AddAsync(customerId, request)`: validate customer exists;
    `AuthorUserId = Guid.Parse(currentUserAccessor.Handle!)`;
    `CreatedAtUtc = DateTimeOffset.UtcNow`; save; audit `"note_added"` with
    `EntityType: "CustomerNote"`, metadata `customerId`.
  - `ListAsync(customerId)`: `AsNoTracking`, ordered by `CreatedAtUtc`
    ascending (chronological).
  - Failure enum: `None, CustomerNotFound`.
- `CustomerManagementModule.cs` — new endpoints under
  `/api/v1/customers/{customerId:guid}/notes`:
  - `POST ""` → `AddNoteAsync`, `.ValidatesDataAnnotations<AddCustomerNoteRequest>()`, `customers.manage`.
  - `GET ""` → `ListNotesAsync`, `customers.view`.
  - Reuse the existing `NotFoundProblem()` helper (`customers.not_found`) for
    `CustomerNotFound`.
- No `Permissions.cs` change — story does not call for a distinct notes
  permission; reuse `customers.manage`/`customers.view`.

## Frontend — extend `customers` feature

- `customers.service.ts`: add `CustomerNote`/`AddCustomerNoteRequest` types
  and `listNotes`/`addNote` methods (mirror existing contact methods).
- `customer-detail.ts/html`: add a "Notes" section after the contacts
  section — `notes`/`notesLoading`/`showNoteForm`/`noteErrorKey` signals, a
  `noteForm` (single `body` textarea, Reactive Forms), `loadNotes()` called
  alongside `loadContacts()` in `load()`, `submitNote()`. `p-table` listing
  Author/Body/CreatedAtUtc chronologically, no row actions (immutable), empty
  state message when no notes exist. Add button gated by
  `authorization.has('customers.manage')`, same as contacts.
- `customer-translations.ts`: add `customers.notes.*` keys (title, add,
  cancel, empty, fields.*, errors.*) in English + Arabic, mirroring
  `customers.contacts.*` naming.

## Tests

- Backend: extend `SquadCrm.Api.Tests/CustomerEndpointsAuthorizationTests.cs`
  with `AddNote_RejectsAnonymousRequest` / `ListNotes_RejectsAnonymousRequest`.
- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerNoteManagementTests.cs`
  covering: add note + audit record written, list returns chronological
  order, add against nonexistent customer returns `CustomerNotFound`.
- Frontend: extend `customer-detail.spec.ts` for the notes section (renders
  list, add flow, permission-gated add button, empty state).

## Verification

- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests).
- EF migration applied cleanly against local Postgres.
- Frontend `ng test` for updated specs; `ng build`/lint.
- Browser smoke: add and view notes in English and Arabic (RTL).
