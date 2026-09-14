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
