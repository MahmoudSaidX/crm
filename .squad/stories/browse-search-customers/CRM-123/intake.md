# CRM-123 — Browse & Search Customers

## Story
As a support user, I want to browse and search customers so that I can
quickly locate the customer I need to support.

## Acceptance Criteria
- Authorized users see a paginated customer list limited to permitted scope.
- Search supports customer number, name and supported contact identifiers.
- Department, branch and status filters are available where applicable.
- Sorting and pagination are deterministic.
- Selecting a result opens the customer profile.

## Business Rules
- Organizational scope is enforced before search/filter results are returned.
- Search must not expose hidden customer data through suggestions/counts.
- Default sorting is deterministic.

## Fields
SearchText (string), DepartmentIds (UUID[]), BranchIds (UUID[]), Status
(enum[]), SortBy (supported field), SortDirection (Asc/Desc), Page (>=1),
PageSize (bounded positive integer).

## Scope note (deadline override)
Practical list with basic pagination/search and only filters already easy
to support; essential authorization remains.

- **Search fields**: CustomerNumber, FirstName, LastName only — "supported
  contact identifiers" do not exist yet on the `Customer` entity (contact
  details arrive in CRM-126); nothing to search there today.
- **Filters**: DepartmentId, BranchId, Status — all already on `Customer`.
- **Organizational scope**: `ICurrentUserAccessor` (CRM-110) is deliberately
  narrow — authenticated flag + opaque handle only, no department/branch/org
  scope model. No existing list endpoint (Branches, Departments, StaffUsers)
  implements caller-scoped filtering because that model does not exist
  anywhere in the codebase yet. CRM-123 therefore enforces the same
  authorization shape as every other list endpoint: a dedicated
  `customers.view` permission gate, consistent with `branches.view`/
  `departments.view`. Introducing real per-caller org-scope filtering here
  would mean designing that model — an architecture decision belonging to
  whichever story first needs it, not a CRM-123 addition.
- **"Selecting a result opens the customer profile"**: CRM-124 ("View
  Customer Profile") is a separate, not-yet-implemented backlog story and
  owns the full profile view. CRM-123 adds a minimal read-only detail route
  (`/customers/:id`) showing only the fields the `Customer` entity already
  has (number, name, language, department, branch, status) so the AC's
  navigation is satisfied without pre-building CRM-124's scope.
- New `GET /api/v1/customers` endpoint + `GET /api/v1/customers/{id}` on the
  existing `CustomerManagement` module, following the `BranchManagement`
  list pattern (`PaginationRequest`/`PagedResult<T>`).
