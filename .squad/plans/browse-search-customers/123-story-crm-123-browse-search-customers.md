# Plan — CRM-123 Browse & Search Customers

Branch: `feat/crm-123-browse-search-customers`

## Backend (`SquadCrm.Modules.CustomerManagement`)
1. `Permissions.cs`: add `CustomersView = "customers.view"` (+ policy const).
2. `RoleManagement` module: register `customers.view` permission
   - `Permissions.cs` + `PermissionPolicies.cs`: add `CustomersView`.
   - `RoleManagementModule.cs`: register the `permission:customers.view` policy.
   - New migration `AddCustomerViewPermission` inserting the `customers.view`
     row into `permission_definition` (mirrors `AddCustomerPermissions`).
3. `CustomerContracts.cs`: add `CustomerListQuery` (search, departmentIds,
   branchIds, status, sortBy, sortDirection — bound via `[AsParameters]`
   alongside `PaginationRequest`) and reuse existing `CustomerResponse`.
4. `CustomerService.cs`: add
   - `ListAsync(CustomerListQuery, PaginationRequest, ct)` — filters by
     search text (CustomerNumber/FirstName/LastName, case-insensitive via the
     existing Normalized* columns + CustomerNumber), DepartmentId, BranchId,
     Status; deterministic default sort (CustomerNumber asc) plus supported
     SortBy (CustomerNumber, FirstName, LastName, CreatedAtUtc) forced with a
     stable tiebreaker (Id) so pagination never reorders across pages.
   - `GetAsync(Guid id, ct)` — single lookup for the detail route.
5. `CustomerManagementModule.cs`: add
   - `GET /api/v1/customers` → `ListAsync`, gated by `customers.view`.
   - `GET /api/v1/customers/{id:guid}` → `GetAsync`, gated by `customers.view`,
     404 problem on miss (mirrors Branches `NotFoundProblem`).

## Frontend (`agent-crm`)
1. `customers.service.ts`: add `list(query, page, pageSize)` and `get(id)`.
2. New `customer-list.ts/.html/.scss` (+ spec) modeled on `branch-list.*`:
   PrimeNG `p-table`, lazy pagination, search input + department/branch/status
   filters, row click navigates to `/customers/:id`.
3. New `customer-detail.ts/.html/.scss` (+ spec): minimal read-only display
   of the fields already returned by `CustomerResponse`.
4. `app.routes.ts`: add `customers` (list, `customers.view`) and
   `customers/:id` (detail, `customers.view`) routes; keep `customers/new`
   ordered before `customers/:id` so `new` does not get captured as an id.
5. `customer-translations.ts`: add list/detail/filter/empty/sort strings
   (en + ar).
6. Any nav/menu entry that already links to `branches`/`departments` gets a
   matching `customers` entry, if one exists for those modules.

## Verification
- Backend: unit/integration tests for `CustomerService.ListAsync` (search,
  each filter, sort, deterministic pagination) and the new endpoints
  (authorization gate, 404 on detail).
- Migration applies cleanly (`dotnet ef database update` / existing
  migration-check tooling).
- Frontend: component spec for list (loads page, applies filters, row
  navigation) and detail (renders fields, 404/not-found handling).
- Build/type-check/lint both projects.
- Browser smoke: list loads, search/filter/sort/pagination work, row click
  opens detail, EN/AR + RTL/LTR render, desktop/mobile do not break.
