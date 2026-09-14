# Plan — URL Query State + API Query Handling + Input Security

Two coupled concerns in one PR: the URL becomes the source of truth for
list state, and the HTTP/query boundary that URL feeds is validated
server-side. Built on the merged internal module architecture (ADR-012).

**Layer classification of new backend code (per the implementation-workflow
rule).** Everything added here is **Presentation**: request records and the
validation filters attached to existing routes. No Domain type, no Application
service signature, no Infrastructure type and no cross-module Contract changes.

- **Endpoint ownership** — unchanged. Every route keeps its module, path, verb
  and permission; only validation filters and two new `[AsParameters]` binding
  records are added.
- **Domain ownership** — unchanged. No entity, policy or domain event touched.
- **Persistence ownership** — unchanged. **Migration expected: NONE.**
- **Cross-module dependencies** — none added.
- **Event/outbox involvement** — none.
- **Background-processing involvement** — none.

## Frontend inventory (all of `agent-crm`; the customer portal has no list screens)

| Screen / route | Search | Filters | Pagination | Sorting | API query contract | Current URL params | Migration |
| -- | -- | -- | -- | -- | -- | -- | -- |
| `/tickets` | signal, submit (Enter + button) | categoryId, priorityId, departmentId, branchId, channel (single-select each) | lazy, `pageSize` fixed 20 | server: `sortBy` ∈ TicketNumber\|CreatedAtUtc\|UpdatedAtUtc, `sortDirection`; **no UI** | `GET /api/v1/tickets` — `search`, `categoryIds[]`, `priorityIds[]`, `departmentIds[]`, `branchIds[]`, `channels[]`, `assignedToMe`, `sortBy`, `sortDirection`, `page`, `pageSize` | none | full |
| `/customers` | signal, submit | departmentId, branchId | lazy, fixed 20 | server: `sortBy` ∈ CustomerNumber\|…, `sortDirection`; no UI | `GET /api/v1/customers` — `search`, `departmentIds[]`, `branchIds[]`, `status[]`, `sortBy`, `sortDirection`, `page`, `pageSize` | none | full |
| `/tasks` | signal, submit | status, dueFilter (`overdue`/`dueToday` → `dueBefore`/`dueAfter`) | lazy, fixed 20 | server: `sortBy`/`sortDirection`; no UI | `GET /api/v1/agent-tasks` — `search`, `statuses[]`, `dueBefore`, `dueAfter`, `myTasksOnly`, `sortBy`, `sortDirection`, `page`, `pageSize` | none | full |
| `/staff-users` | signal, submit | — | lazy, fixed 20 | none | `GET /api/v1/staff-users` — `search`, `page`, `pageSize` | none | full |
| `/audit` | — | entityType, action, actorHandle (free text, submit) | lazy, fixed 20 | none | `GET /api/v1/audit-records` — `entityType`, `action`, `actorHandle`, `from`, `to`, `page`, `pageSize` | none | full |
| `/quick-replies` | — | — | lazy, fixed 20 | none | `GET /api/v1/quick-replies` — `page`, `pageSize` | none | page only |
| `/branches` | — | — | lazy, fixed 20 | none | `GET /api/v1/branches` | none | page only |
| `/departments` | — | — | lazy, fixed 20 | none | `GET /api/v1/departments` | none | page only |
| `/roles` | — | — | lazy, fixed 20 | none | `GET /api/v1/roles` | none | page only |
| `/ticket-categories` | — | — | lazy, fixed 20 | none | `GET /api/v1/ticket-categories` | none | page only |
| `/ticket-priorities` | — | — | lazy, fixed 20 | none | `GET /api/v1/ticket-priorities` | none | page only |
| `/system-configuration` | — | — | none (fixed catalog) | none | `GET /api/v1/configuration` | none | **excluded** — no list state to address |

Backend parameter names are reused verbatim. Nothing in the API contract is
renamed.

## URL contract

`page` (default 1) · `pageSize` (default 20) · `search` · screen-specific
filters, each named after the backend parameter it feeds but singular where the
UI offers a single select (`categoryId` → `categoryIds[]` on the wire) ·
`sort` + `dir` where server sorting exists.

Defaults and empty values are **omitted**:
`/tickets?search=payment&priority=<id>&page=3`.

Flow, in one direction only:

```
URL query params → parse → validate/normalize → list state → API request
UI change        → router.navigate(queryParams) → (the line above)
```

Components never call `load()` directly from a UI handler and never hold list
state that the URL does not carry, so state cannot drift from the URL and
Back/Forward work for free.

## Frontend implementation

A small typed utility in `@squad-crm/platform` — justified by eleven screens
repeating the same parse/normalize/navigate code, and kept deliberately narrow
(no generic query-state framework):

- `projects/platform/src/lib/list-state/list-query.ts`
  - `readPage(map)` / `readPageSize(map)` — bounded **plain-decimal** parsing;
    anything non-numeric, `<= 0`, or `> 200` falls back to the default rather
    than throwing. Stricter than `Number()` on purpose: that accepts `1e3` as
    1000 and `0x10` as 16, so the URL and the state it produces would disagree
    about how a page is written.
  - `readText(map, key, maxLength)` — trims, drops empty, truncates at the
    backend's own maximum. **No character filtering** — Arabic, Unicode,
    apostrophes and angle brackets pass through unchanged.
  - `readEnum(map, key, allowed)` — returns `null` for anything not in the
    allow-list.
  - `readGuid(map, key)` — shape check only; the backend re-validates.
  - `toQueryParams(state, defaults)` — omits defaults, `null`, `''`.
- `projects/platform/src/lib/list-state/list-url-state.ts` — `ListUrlState<T>`,
  created with `injectListUrlState(spec)`: exposes a `state` signal fed by
  `route.queryParamMap`, deduplicated on the serialized param set so an
  identical navigation issues no second request, and `patch(changes)` /
  `setPage(page)` helpers that navigate. `patch` resets `page` to 1; `setPage`
  does not.

Each list component then: binds its inputs to signals for typing UX (unchanged),
calls `patch({...})` from `onFilter()`/`onSearch()` and `setPage(n)` from the
table's paginator, and loads from an `effect` on `state`. `[first]` is bound on
the PrimeNG table so the paginator itself reflects a deep-linked page.

**Paging is driven by `(onPage)`, not `(onLazyLoad)`** (found while testing).
`onLazyLoad` also fires on init and every time `[value]` is reassigned — and
each load assigns a fresh array — so with the URL bound to `[first]` the table
echoed `first: 0` back after every page change and bounced the URL to page 1.
`(onPage)` is emitted only by the paginator, which is exactly the user action
being modelled.

`pageSize` and `sort` are parsed, normalized and honoured, but **no new UI
control is added** for either — none exists today and adding one would be a
silent UX redesign. Deep links carrying them work.

## Backend hardening (Presentation layer only)

1. **Pagination bounds on every collection list route.** Attach
   `.ValidatesDataAnnotations<PaginationRequest>()` to the 15 `MapGet("")` list
   endpoints that take a `PaginationRequest`. This is the fix for the
   `?page=0` → `Skip(-20)` → 500 defect and the unbounded `pageSize`. The
   remaining two `MapGet("")` routes — system configuration and branding
   settings — serve fixed catalogs with no pagination argument at all and are
   correctly left alone.
2. **Search/filter length bounds.** `[MaxLength(200)]` on `Search` in
   `TicketListQuery`, `CustomerListQuery`, `AgentTaskListQuery`,
   `QuickReplyListQuery`, with `.ValidatesDataAnnotations<…ListQuery>()`
   attached.
3. **Two new `[AsParameters]` binding records** for the endpoints that still take
   loose `string?` parameters, preserving the exact wire parameter names:
   `AuditListQuery(EntityType, Action, ActorHandle, From, To)` and
   `StaffUserListQuery(Search)`, each with `[MaxLength(200)]`.
4. **Filter cardinality bounds.** `[MaxLength(50)]` on the `Guid[]`/enum[] filter
   arrays, so one request cannot build an unbounded `IN` list.
5. **Sort allow-list** — already structural: `SortBy`/`SortDirection` are C#
   enums, so an unknown value fails model binding with a 400. Covered by tests
   rather than new code.
6. **Raw SQL** — audited: one site, constant text, `NpgsqlParameter` values, no
   user input. No change.
7. **`BadHttpRequestException` must keep its own status** (found by the new
   tests, not by inspection). `GlobalExceptionHandler` mapped *every* escaping
   exception to 500, including ASP.NET Core's own binding failure — which
   already carries 400. So `?page=abc`, `?sortBy=bogus`, `?status=NotAReal`
   and `?departmentIds=not-a-guid` each reported a server outage for what is a
   client typo. The handler now honours that status, logs it at warning rather
   than error, and still never writes the exception message to the body. This
   is the "malformed URL state must not cause a 500" requirement, and it is a
   cross-cutting BuildingBlocks change rather than a per-module one.

Deliberately **not** done: no global sanitizer, no character stripping, no
HTML-encoding of stored data, no regex HTML sanitizer, no `bypassSecurityTrust`
anywhere (none exists to remove).

## Tests

**Frontend** (representative migrated screens — customers is the reference
implementation and carries the full matrix; tickets, tasks and audit cover
filters, enum allow-lists and sort):

query params → API request · page change → URL + API · pageSize change → URL +
API · search → URL + API + page reset · filter → URL + API + page reset · sort
from URL → API · deep-link reconstruction · Back/Forward · malformed
`page`/`pageSize` · unknown enum/sort value · Arabic search · Unicode search ·
filter preservation across pagination · no duplicate request on an identical
navigation. Plus unit specs for the `list-query` helpers.

**Backend** (`SquadCrm.Api.Tests` for contract/status, integration tests for
data behaviour): `page=0` / `page=-1` / `page=abc` · `pageSize=0` /
`pageSize=201` / `pageSize=100000` · 201-character search · unknown enum filter
· unknown sort field · malformed Guid filter · apostrophe search · Arabic search
· Unicode search · HTML-looking search stored and returned unchanged ·
SQL-injection-looking search returning no rows and altering nothing. Assertions
are on real HTTP status and body, never on "a sanitizer was called".

## CI gap found during verification

`npm test` — the exact command CI runs — executes `ng test` with no project
argument, which runs a **single** project. It was running `@squad-crm/shared-ui`
only: 15 specs. Every `agent-crm`, `@squad-crm/platform` and `customer-portal`
spec in the repository, including all of this story's, was never executed by CI.
`package.json`'s `test` script now runs all four projects (326 specs). This is
in scope because a test that CI never runs protects nothing, and the story's own
acceptance depends on those specs running.

## Rules updates

- `CLAUDE.md` + `AGENTS.md` — new `## List State, Query Parameters, and Input
  Security` section (15 short rules).
- `docs/development/implementation-workflow.md` — list-screen and new-input
  planning requirements (URL state, API query contract, defaults, invalid-query
  behaviour, search debounce/submit, Back/Forward/deep-link expectations;
  input fields, backend validation, normalization, output/rendering context,
  XSS and SQL/dynamic-query considerations). No "sanitize inputs" checkbox.

## Verification

Frontend: `npm test`, `npm run lint`, `npm run format:check`, production builds
for both apps. Backend: full `dotnet test` (unit, API, integration,
architecture), `dotnet format --verify-no-changes`, `has-pending-model-changes`
per context. Browser smoke on `/tickets` and `/customers`: search → filter →
page 2 → inspect URL → refresh → Back → Forward → paste URL in a new tab, in
EN/LTR and AR/RTL including an Arabic search term, watching the network tab for
correct parameters and no duplicate calls, and the console for errors.

**Database changes: NONE.**
