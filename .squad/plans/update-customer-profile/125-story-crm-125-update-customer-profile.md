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

## Deviation — post-approval, publication-verification fix

Publication verification (after initial merge approval) discovered that
`CustomerContracts.cs` never applied `[JsonConverter(typeof(JsonStringEnumConverter))]`
to `CustomerStatus`/`CustomerPreferredLanguage` (only `CustomerContactType`,
from CRM-126, had it), and no global enum-string converter is registered
anywhere in the API. System.Text.Json's default behavior for an undecorated
enum is to serialize/deserialize it as an **integer**, not its name. This
pre-existing contract defect (present since CRM-122 for `PreferredLanguage`)
was invisible until now because `CustomerStatus` only ever had one value and
no test exercised a real authenticated HTTP round trip. CRM-125 made it
release-blocking: `Status` became a genuinely two-valued, user-editable field
sent by the frontend as `"Active"`/`"Inactive"` strings, which the backend
would have rejected (400 on bind) or misrepresented (integers on read) over
the real wire.

**Fix, scoped intentionally to `CustomerManagement` contracts only:**
`[property: JsonConverter(typeof(JsonStringEnumConverter))]` added to
`CustomerStatus`/`CustomerPreferredLanguage` on `CreateCustomerRequest`,
`UpdateCustomerRequest`, and `CustomerResponse` — the same per-property
pattern already established by `CustomerContactType`. No global
`JsonStringEnumConverter` was registered for the API, and no other module's
enums were touched; that remains a separate cross-cutting decision.

New `SquadCrm.Persistence.IntegrationTests/CustomerHttpContractTests.cs` hosts
the real API (`WebApplicationFactory<Program>`) against the same real,
migrated Postgres this test project already owns, with a real seeded staff
user/role/permission grant and a real signed-in JWT — proving the actual JSON
bytes on the wire (not just C# enum equality) for create/get/update. Verified:
`preferredLanguage`/`status` round-trip as names, not integers; the existing
`CustomerContactType` behavior is unchanged.

**A second, separate defect surfaced during this same verification**, also
pre-existing and cross-cutting rather than Customer-specific: an invalid
enum string produces `Microsoft.AspNetCore.Http.BadHttpRequestException`
(which carries its own 400 status), but the shared `GlobalExceptionHandler`
treats every exception identically and always writes a generic 500 —
regardless of enum converters, and for any endpoint in the app. Fixing that
handler is a global exception-handling decision outside this fix's declared
scope, so it was left unfixed and is instead documented by a test that
asserts today's actual (500) behavior, with a recommendation to address it
as its own follow-up.
