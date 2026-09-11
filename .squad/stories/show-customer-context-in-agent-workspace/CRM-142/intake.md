# CRM-142 — Show Customer Context in Agent Workspace

## Story
As a support agent, I want relevant customer context alongside a ticket so
that I can respond without repeatedly navigating away.

## Acceptance Criteria
- Agent viewing an accessible ticket can see a permitted customer
  profile/contact summary alongside it.
- Context includes customer number/name, status, relevant contact methods
  and organizational context according to permission.
- Recent permitted interaction/ticket context is available through the
  Customer interaction capability.
- Agent can navigate to the full customer profile when authorized.
- Customer data refreshes from the canonical Customer capability rather than
  being independently editable/cached as ticket-owned data.
- Missing or restricted fields are handled without leaking their
  existence/value.

## Business Rules
- Customer module owns customer profile/contact data; Agent Workspace
  consumes a permitted read composition.
- Ticket access does not automatically grant every sensitive customer field.
- Internal customer notes/attachments are shown only with their respective
  permissions.
- Customer Portal ownership/security semantics are not reused as staff
  authorization; staff uses Permission + Organizational Scope.

## Scope note
Deadline override: reuse existing Customer Management reads
(`GET /customers/{id}`, `/contacts`, `/timeline`) to render a compact,
read-only summary panel on the existing ticket-detail screen. No new
backend endpoints, no editable/cached ticket-owned customer data, no new
aggregation service.

- All three read endpoints already require `customers.view`
  (`CustomerManagementModule.cs:57-62`) — the backend already authorizes
  every field independently; the frontend adds no new gate, it only stops
  rendering the panel when a read fails (403/404), so missing/restricted
  data never leaks its existence (same swallow-all pattern ticket-detail
  already uses for department/branch labels).
- "Recent permitted interaction/ticket context ... through the Customer
  interaction capability" is satisfied by reusing the existing
  `GET /customers/{id}/timeline` (CRM-129), not a new dashboard-owned feed.
- Internal notes/attachments are explicitly out of scope for this panel —
  they already have their own permission-gated sections on the full customer
  profile screen; this is a summary, not a duplicate.
- "Navigate to full customer profile when authorized" links to the existing
  `/customers/:id` route, itself already guarded by `requirePermission
  ('customers.view')` — the link only ever renders once the panel's own
  fetch already succeeded, so it can't be shown to someone who couldn't load
  it anyway.
