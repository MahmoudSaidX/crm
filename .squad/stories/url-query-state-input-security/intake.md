# Frontend URL Query State + API Query Handling + Input Security

Platform story. No Linear product issue: this changes how existing list
screens carry their state and how existing endpoints validate untrusted
input. It deliberately **does** change list-state behaviour (unlike the
structural refactor in `backend-internal-module-architecture`).

## Story

As an agent using Squad CRM, I want a list's search, filters and page to live
in the browser URL so that refreshing, deep-linking and Back/Forward reproduce
exactly what I was looking at — and as the team, we want every list endpoint to
treat that URL as untrusted input and validate it server-side.

## Problem (current reality, after inspection)

**Frontend.** Eleven paginated list screens in `agent-crm` share one pattern:
component `signal()`s hold `search`/filters, `pageSize` is a fixed `readonly 20`,
and `load(page)` is called from `onLazyLoad`/`onFilter`. Not one of them reads or
writes a URL query parameter. Consequences:

- Refreshing `/tickets` after searching and paging to page 3 returns to page 1
  with empty filters.
- A URL copied to a colleague or another tab reproduces none of the list state.
- Browser Back after paging leaves the route but not the list state, so Back
  navigates away from the screen entirely rather than to the previous page.

**Backend.** `PaginationRequest` already declares `Range(1, int.MaxValue)` on
`Page` and `Range(1, 200)` on `PageSize`, but **no collection list endpoint
attaches `ValidatesDataAnnotations<PaginationRequest>()`** — verified across all
17 `MapGet("")` list routes. The bounds are therefore never enforced:

- `?page=0` reaches `Skip((0 - 1) * 20)` = `Skip(-20)`, which PostgreSQL rejects
  (`OFFSET must not be negative`) — an unhandled 500 from a query string.
- `?pageSize=1000000` is honoured verbatim; one request can pull the whole table.
- `search`, `entityType`, `action` and `actorHandle` have no maximum length.
- Guid/enum array filters (`categoryIds`, `statuses`, …) have no cardinality
  bound.

**Already correct — audited, and not to be "fixed".** Every frontend service
builds requests with Angular `HttpClient` `params` objects; there is no
query-string concatenation anywhere. All filtering/searching goes through EF
Core LINQ, so it is parameterized. The only raw SQL in the codebase
(`ArchitectureFixtureOutboxStore`) is constant text with `NpgsqlParameter`
values and no user input. `sortBy`/`sortDirection` are already C# enums, so the
sortable-field allow-list exists structurally. `grep` for `innerHTML`,
`DomSanitizer`, `bypassSecurityTrust`, `outerHTML`, `insertAdjacentHTML` and
`document.write` across the frontend returns **nothing** — all user text already
renders through Angular interpolation.

## Acceptance Criteria

- For every server-backed list screen, `page`, `pageSize`, `search`, filters and
  (where the server supports it) sort are represented in the URL query string.
- Navigating to a URL parses, validates and normalizes its query parameters into
  list state and issues exactly one API request from that normalized state.
- Refresh preserves list state; a pasted URL reproduces it in another tab;
  Back/Forward restore state **and** results.
- Changing search or any filter resets `page` to 1; changing page preserves
  search and filters.
- Default and empty values are omitted from the URL.
- Malformed query state (`page=abc`, `page=-5`, `pageSize=99999`, unknown enum,
  unknown sort field) produces a normalized, working screen — never a frontend
  or backend crash.
- Backend rejects out-of-range/oversized/unknown list parameters with a 400
  validation problem, not a 500 and not a full-table read.
- Arabic, Unicode, apostrophes, and SQL- or HTML-looking search text are treated
  as ordinary data and round-trip unchanged.

## Business Rules

- **The backend is the authoritative security boundary.** Frontend validation is
  UX only.
- **No global/generic input sanitization.** Nothing strips apostrophes,
  punctuation, HTML characters, Arabic or Unicode. Valid business data is stored
  as entered; encoding is a rendering-context concern.
- SQL-injection protection is parameterized access plus allow-listed query
  structure — never string filtering of input.
- No raw SQL may be built by concatenating search, filter or sort input.
- Dynamic sort fields must come from an explicit allow-list.
- Angular's sanitization must not be bypassed for user-controlled content.
- Existing search interaction style is preserved: these screens submit on Enter
  or the Search button, so no live-search/debounce is introduced.
- No database migration is expected.

## Out of scope

- Adding sorting or page-size UI controls where none exists today (that would be
  a silent UX redesign). Both are parsed from, and honoured via, the URL.
- The customer portal, which has no server-backed list screens.
- `system-configuration-list`, which loads a small fixed catalog with no page,
  search or filter.
