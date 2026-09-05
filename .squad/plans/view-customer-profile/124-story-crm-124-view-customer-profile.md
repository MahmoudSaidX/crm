# Plan — CRM-124 View Customer Profile

Branch: `feat/crm-124-view-customer-profile`

## Reconciliation finding

Full implementation already landed as part of CRM-123 (browse/search), since
that story's AC "selecting a result opens the customer profile" already
required the detail endpoint + screen:

- Backend: `GET /api/v1/customers/{id:guid}` → `CustomerService.GetAsync`,
  gated by `customers.view`, 404 problem on miss.
- Frontend: `CustomerDetail` at route `customers/:id`, showing identity,
  department/branch, preferred language, status; loading/not-found states;
  component spec covers success + 404.
- Contact summary / notes / attachments / interaction history sections are
  explicitly deferred by the story text ("as their capabilities become
  available") — CRM-126 (contacts) is not yet built, so there is nothing to
  integrate yet. Not a gap for this story.
- Organizational-scope filtering (beyond the `customers.view` permission
  gate) is not implemented for `GetAsync`, consistent with `ListAsync` in the
  already-Done CRM-123 — no new scope infrastructure is introduced here to
  keep behavior consistent (YAGNI); revisit only if CRM-123's scope model
  changes.

## Remaining gap

Only test coverage: `CustomerService.GetAsync` has a persistence-layer test
for the unknown-id case (`Get_UnknownId_ReturnsNull`) but none for the
happy path. Per the repo's established pattern (`CustomerEndpointsAuthorizationTests`
only covers the 401 auth boundary; success-path/field correctness is covered
at the persistence-integration layer), the missing piece is a happy-path
`GetAsync` test.

## Task

- `CustomerManagementTests.cs`: add `Get_ExistingCustomer_ReturnsCustomer`
  asserting `GetAsync` returns the created customer's fields.

## Verification

- Run the new/affected persistence integration test.
- No frontend/backend behavior changed; no build/lint changes needed beyond
  compiling the new test.
