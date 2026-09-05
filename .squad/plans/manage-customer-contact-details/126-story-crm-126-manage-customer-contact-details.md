# Plan — CRM-126 Manage Customer Contact Details

Branch: `feat/crm-126-manage-customer-contact-details`

New sub-resource under the existing `CustomerManagement` module (same schema
`customer_management`, same `CustomerManagementDbContext`) — no new module,
mirroring how `CustomerService`/`CustomerManagementModule` already handle the
`Customer` entity.

## Backend — `CustomerManagement` module additions

- `Persistence/CustomerContact.cs`
  - `Id`, `CustomerId` (FK to `Customer.Id`), `Type` (`CustomerContactType`:
    Email/Phone), `Value`, `NormalizedValue`, `Label?`, `IsPrimary`,
    `IsActive` (default true), `VerifiedAtUtc?` (always null — no
    verification workflow yet), `CreatedAtUtc`, `UpdatedAtUtc`.
- `CustomerManagementDbContext`: add `DbSet<CustomerContact> CustomerContacts`;
  configure FK to `Customer`, and a partial unique index on
  `(CustomerId, Type)` where `IsPrimary = true AND IsActive = true` (enforces
  "at most one active primary per type per customer" at the DB layer, mirrors
  the existing partial/match-column index precedent used for `Customer`
  duplicate detection).
- EF migration `AddCustomerContacts`.
- `CustomerContracts.cs` additions:
  - `AddCustomerContactRequest(CustomerContactType Type, string Value, string? Label, bool IsPrimary)`
  - `UpdateCustomerContactRequest(string Value, string? Label, bool IsPrimary)` (Type immutable, same precedent as `CustomerNumber`)
  - `CustomerContactResponse(Guid Id, Guid CustomerId, CustomerContactType Type, string Value, string? Label, bool IsPrimary, bool IsActive, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc)`
  - `DeactivateCustomerContactRequest(Guid? NewPrimaryContactId)` — required only when other active contacts of the same type remain.
- `CustomerContactService.cs` (new, mirrors `CustomerService` conventions):
  - `AddAsync`: validate customer exists; validate+normalize `Value` by
    `Type` (email: `EmailAddressAttribute`-style format + lowercase/trim;
    phone: digits-only after stripping separators, min-length check); if
    `IsPrimary`, unset the customer's existing active primary of that type in
    the same transaction; audit `"contact_added"`.
  - `UpdateAsync`: validate+normalize `Value`; same primary-swap handling as
    Add when `IsPrimary` transitions to true; audit `"contact_updated"`.
  - `DeactivateAsync`: set `IsActive = false`; if the contact was primary and
    other active contacts of the same type exist, require a valid
    `NewPrimaryContactId` (same customer, same type, active, not the one
    being deactivated) and promote it to primary — otherwise return a
    `RequiresNewPrimary`/`InvalidNewPrimary` failure; audit
    `"contact_deactivated"`.
  - `ListAsync(customerId)`: returns active + inactive contacts for the
    customer's detail view (ordered by Type, then CreatedAtUtc).
  - Failure enum: `None, CustomerNotFound, ContactNotFound, InvalidValue, RequiresNewPrimary, InvalidNewPrimary`.
- `CustomerManagementModule.cs` — new endpoints under
  `/api/v1/customers/{customerId:guid}/contacts`:
  - `POST ""` → `AddAsync`, `customers.manage`.
  - `GET ""` → `ListAsync`, `customers.view`.
  - `PUT "/{contactId:guid}"` → `UpdateAsync`, `customers.manage`.
  - `POST "/{contactId:guid}/deactivate"` → `DeactivateAsync`, `customers.manage`.
  - Problem responses mirror existing `customers.*` codes:
    `customers.not_found` (customer or contact), `customers.contacts.invalid_value`,
    `customers.contacts.requires_new_primary`, `customers.contacts.invalid_new_primary`.

## Frontend — extend `customers` feature

- `customers.service.ts`: add `CustomerContact`/`CustomerContactType` types
  and `listContacts`, `addContact`, `updateContact`, `deactivateContact`
  methods (mirror existing `get`/`create`).
- `customer-detail.ts/html`: add a "Contacts" section — table of contacts
  (Type, Value, Label, Primary badge, Active/Inactive), an "Add contact"
  action opening a small reactive form (PrimeNG `Dialog` + `InputText` +
  `Select` + `Checkbox`), row-level Edit/Deactivate actions. Mutation actions
  rendered only when `authorization.state.has('customers.manage')` (existing
  pattern from other management screens); missing contacts render an empty
  state message (BR: "Missing optional information is handled clearly").
  Deactivating a primary contact with other active contacts of the same type
  prompts for the replacement primary before submitting.
- `customer-translations.ts`: add contact-section keys (add/edit/deactivate
  labels, type labels, primary badge, empty state, error keys for the new
  problem codes) in English + Arabic.

## Tests

- Backend: `SquadCrm.Api.Tests/CustomerContactEndpointsAuthorizationTests.cs`
  (anonymous 401 on all four new endpoints, mirrors
  `CustomerEndpointsAuthorizationTests`).
- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerContactManagementTests.cs`
  covering: add + normalization by type, primary swap on add/update, list,
  deactivate without primary conflict, deactivate requiring new primary
  (missing/invalid/valid), audit records written.
- Frontend: extend `customer-detail.spec.ts` for the contacts section
  (renders list, add/edit/deactivate flows, permission-gated actions, empty
  state).

## Verification

- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests).
- EF migration applied cleanly against local Postgres; strip UTF-8 BOM from
  generated migration files before commit (known CI formatting issue, see
  commit ac03a11).
- Frontend `ng test` for updated specs; `ng build`/lint.
- Browser smoke: add/edit/deactivate a contact in English and Arabic (RTL),
  verify primary-swap and "requires new primary" prompts.
