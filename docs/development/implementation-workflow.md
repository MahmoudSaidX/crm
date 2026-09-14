# Implementation Workflow

1.  Select next unblocked Ready issue in Linear.
2.  Create/update its Squad Kit intake under the structure expected by
    the installed Squad Kit version.
3.  Put required screenshots/PDFs/exports in that story's
    `attachments/`.
4.  Generate and review the Squad Kit plan.
5.  Use Superpowers skills when applicable.
6.  Split into subtasks only for independently reviewable slices.
7.  Move In Progress when coding starts; In Review when ready; Done only
    after DoD.

Never make `.squad/` a second permanent copy of the full Linear backlog.

## What a backend plan must state

Reference `docs/adr/ADR-012-internal-module-layering.md`; do not restate
it. Document only this story's decisions:

-   Every new or moved type classified as **Domain**, **Application**,
    **Presentation**, **Infrastructure** or **cross-module Contract**.
-   **Endpoint ownership** --- which module owns each route, and the
    `Presentation/Endpoints` file it lands in.
-   **Domain ownership** --- which module owns each entity/policy.
-   **Persistence ownership** --- which module's `DbContext` and schema
    change, and **whether a migration is expected** (state "none" when
    none is).
-   **Cross-module dependencies** --- which `*.Contracts` or events are
    consumed or published, and why a direct dependency is not used.
-   **Event/outbox involvement** --- domain events raised, integration
    events published, outbox rows written; or explicitly none.
-   **Background-processing involvement** --- Hangfire jobs added or
    changed, and which Application workflow they trigger; or none.

A plan that introduces a repository, MediatR, CQRS, a generic
`UnitOfWork` or a new architectural package must justify it against an
actual requirement or ADR, or drop it.

## What a list-screen plan must state

For every story with a server-backed list/search screen:

-   **URL state** --- the exact `page`, `pageSize`, `search`, filter and
    sort parameter names this screen puts in the address bar.
-   **API query contract** --- the backend parameter each one feeds, and
    where the two names differ, why (a single-select UI filter feeding a
    plural array parameter is the usual reason). Reuse the established
    backend names; do not rename a contract for aesthetics.
-   **Defaults** --- which values are omitted from the URL.
-   **Invalid-query behaviour** --- what an out-of-range page, an unknown
    enum, a malformed id or an over-long search does, on both sides.
-   **Search debounce/submit behaviour** --- and, if it differs from the
    screen's current interaction style, the justification. Do not
    silently redesign UX.
-   **Back/Forward/deep-link expectations.**

## What a plan accepting new input must state

Not a "sanitize inputs" checkbox --- a context-specific decision per
field:

-   **Input fields** --- name, type, source (route param, query param,
    body, header, uploaded file metadata).
-   **Backend validation** --- required, lengths, ranges, allowed enum
    and filter values, sortable-field allow-list, id format. The backend
    is authoritative; frontend validation mirrors it for UX only.
-   **Normalization** --- trimming, case folding, canonical form. Never
    character stripping: apostrophes, Arabic, Unicode and
    HTML-looking text are valid business data.
-   **Output/rendering context** --- where the value is rendered, and
    through which escaping path.
-   **XSS considerations** --- confirm interpolation. If a field
    genuinely needs rich HTML, name the allow-list sanitizer; a
    regex-based one is not acceptable.
-   **SQL/dynamic-query considerations** --- confirm parameterized
    access, and name the allow-list backing any dynamic sort or filter
    structure.
