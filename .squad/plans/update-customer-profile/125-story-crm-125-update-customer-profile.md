# Plan — CRM-125 Update Customer Profile

Branch: `feat/crm-125-update-customer-profile`

Extends the existing `CustomerManagement` module's `Customer` entity/service/
endpoints — no new module.

## Backend — `CustomerManagement` module additions

- `Persistence/Customer.cs`: add `CustomerStatus.Inactive`.
- `CustomerManagementDbContext`: configure `Customer` entity with
  `.UseXminAsConcurrencyToken()` (shadow property over the Postgres system
  column `xmin`) — no migration needed, no new column.
- `CustomerContracts.cs` additions:
  - `UpdateCustomerRequest(string FirstName, string LastName, CustomerPreferredLanguage? PreferredLanguage, Guid? DepartmentId, Guid? BranchId, CustomerStatus Status, uint Version)`
  - `CustomerResponse`: add `uint Version` (from the `xmin` shadow property)
    so the client can round-trip it back on update.
- `CustomerService.cs`:
  - `UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken)`:
    load tracked entity by id (`NotFound` if missing); validate
    department/branch active exactly like `CreateAsync` (`InactiveDepartment`/
    `InactiveBranch`); set the entry's original `xmin` shadow value from
    `request.Version` before save so a mismatch raises
    `DbUpdateConcurrencyException`; update mutable fields (`FirstName`,
    `LastName`, `NormalizedFirstName`/`NormalizedLastName` recomputed,
    `PreferredLanguage`, `DepartmentId`/`BranchId` + their `*MatchId` mirrors,
    `Status`), `UpdatedAtUtc = UtcNow`; catch `DbUpdateConcurrencyException`
    → `ConcurrencyConflict` failure; audit `"updated"` (mirrors `"created"`
    audit call) on success.
  - Extend `CustomerMutationFailure` with `NotFound`, `ConcurrencyConflict`.
- `CustomerManagementModule.cs`:
  - `PUT "/{id:guid}"` → `UpdateAsync`, `ValidatesDataAnnotations<UpdateCustomerRequest>()`,
    `customers.manage`.
  - Map failures: `NotFound` → existing `NotFoundProblem()`; `ConcurrencyConflict`
    → 409 with `code: "customers.update_conflict"`; reuse existing
    `DuplicateProblem`/`InactiveReferenceProblem` helpers are not applicable
    here (update doesn't re-check name/scope duplication — BR doesn't require
    it, only Create's uniqueness index applies at insert time).

## Frontend — extend `customers` feature

- `customers.service.ts`: `Customer.status` becomes `'Active' | 'Inactive'`,
  add `version: number`; add `UpdateCustomerRequest` interface; add
  `update(id, request): Promise<Customer>` (PUT).
- `customer-detail.ts/html`: add an "Edit" action (gated on
  `authorization.has('customers.manage')`) that toggles a reactive form
  pre-filled from the loaded customer (mirrors `CustomerForm`'s
  firstName/lastName/preferredLanguage/department/branch controls, plus a
  Status `p-select` Active/Inactive). Submit calls `update` with the
  in-memory `version`; on success, reloads the customer and exits edit mode.
  On `customers.update_conflict`, show a conflict message and reload the
  latest customer so the next save uses the current version (satisfies "a
  clear conflict response" without building merge/diff UX).
- `customer-translations.ts`: add edit-section keys (edit action reuses
  existing `common.actions.edit`/`common.actions.save`/`common.actions.cancel`;
  add `customers.errors.updateConflict`, status option labels reuse existing
  `common.status.active`/`common.status.inactive`).

## Tests

- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerManagementTests.cs`
  — add cases: update succeeds + audited, NotFound, inactive department/branch
  rejected, concurrency conflict on stale version, CustomerNumber immutability
  (not settable via request shape).
- Backend: `SquadCrm.Api.Tests/CustomerEndpointsAuthorizationTests.cs` — add
  `Update_RejectsAnonymousRequest`.
- Frontend: extend `customer-detail.spec.ts` for edit flow (renders form,
  submits update, permission-gated, conflict error path).

## Verification

- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests).
- Frontend `ng test` for updated specs; `ng build`/lint.
- Browser smoke: edit a customer's fields/status in English and Arabic (RTL),
  verify immediate reflect and a conflict message on stale-version resubmit.
