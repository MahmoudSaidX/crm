# Plan — CRM-142 Show Customer Context in Agent Workspace

Branch: `feat/crm-142-show-customer-context-in-agent-workspace`

Frontend-only. No backend change: `GET /customers/{id}`, `GET
/customers/{id}/contacts` and `GET /customers/{id}/timeline` already exist
and already require `customers.view` (`CustomerManagementModule.cs:57-62`).

## Frontend — `src/app/tickets/ticket-detail.ts` / `.html`

- Replace the current `customerName`-only resolution with a single
  `customer` signal (`Customer | null`) plus `customerContacts` (`readonly
  CustomerContact[]`) and `customerTimeline` (`readonly
  CustomerTimelineEvent[]`, capped to the 5 most recent entries).
- New `customerDepartmentName` / `customerBranchName` signals, resolved from
  `customer().departmentId` / `.branchId` via the existing
  `departmentsService` / `branchesService` (distinct from the ticket's own
  `departmentName`/`branchName`, which stay as-is — those describe the
  ticket's routing department, not the customer's).
- `loadCustomerContext(customerId)`: fetches customer, contacts and timeline
  in parallel via `Promise.all`, each independently wrapped so one failure
  doesn't blank the others (mirrors the existing `resolve()` helper). If the
  `customer` fetch itself fails (403/404), all three signals stay `null`/
  empty and the whole panel renders nothing — no separate "restricted"
  message, so a caller without `customers.view` sees no trace the panel
  ever existed.
- Called from `load()` alongside `loadReferenceLabels`/`loadHistory`.
- New template section (after the existing header, before the status/
  assignment actions — same `p-card` + `sc-detail-grid` vocabulary as
  `customer-detail.html`): customer number, name, status (`p-tag`,
  success/danger on Active/Inactive — same mapping as
  `customer-detail.html:151-157`), department/branch name, primary
  email/phone from `customerContacts` (filter `isActive && isPrimary`, one
  per `CustomerContactType`), and a short list of the most recent timeline
  entries (`summary` + `occurredAtUtc`, reusing the `DatePipe` already
  imported).
- "View full profile" link: `routerLink="/customers/:id"` shown only when
  `customer()` is non-null (route itself is guarded by
  `requirePermission('customers.view')`, consistent with the fetch having
  already succeeded).
- No new permission check needed in the template — visibility is entirely
  driven by whether the fetch succeeded, per the intake's leak-avoidance
  requirement.

## Tests

- `ticket-detail.spec.ts`: customer context panel renders number/name/
  status/contacts/recent timeline when the fetch succeeds; panel is absent
  when the customer fetch 403s/404s; "view full profile" link present only
  in the success case.

## Verification

- `ng test` for the updated spec.
- `ng build` / lint.
- Browser smoke: open a ticket as an agent with `customers.view` (panel
  shows, link works) and confirm the panel silently disappears when that
  permission is removed; check English/Arabic RTL layout.
