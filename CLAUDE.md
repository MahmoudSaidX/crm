# Squad CRM --- Claude Code Instructions

## Sources of truth

Linear owns product requirements. `.squad/` owns the active story plan.
`docs/adr/` owns cross-cutting architecture. Code owns current reality.
Surface conflicts; never silently resolve them.

## Squad Kit + Superpowers

Follow the active Squad Kit plan and use installed Superpowers skills
when applicable. This file defines constraints, not a competing
orchestration workflow. Work one Ready story at a time; do not copy the
entire future backlog into `.squad/`. Do not invent missing business
requirements.

## Architecture

-   ASP.NET Core modular monolith designed for later extraction when
    justified.
-   PostgreSQL + EF Core; schema-per-module.
-   No direct access to another module's private tables/DbContext.
-   Explicit contracts and domain/integration events; durable
    integration uses transactional outbox/idempotency.
-   Hangfire is execution infrastructure, not business state.
-   External AI/Email/WhatsApp/SMS/ERP/storage providers stay behind
    provider-neutral ports/adapters.
-   Core CRM workflows must work when optional AI/providers are
    unavailable.

## Backend Module Internal Architecture

See `docs/adr/ADR-012-internal-module-layering.md`. Rules for all future
backend work:

1.  The backend remains a Modular Monolith.
2.  Business modules are the primary architecture boundary.
3.  Place new module code by responsibility: `Domain`, `Application`,
    `Presentation`, `Infrastructure`.
4.  HTTP endpoints, requests and responses belong to `Presentation`.
5.  Business entities and domain behavior belong to `Domain`.
6.  Use-case orchestration belongs to `Application`.
7.  EF Core, migrations, Outbox technical implementation, Hangfire jobs
    and technical adapters belong to `Infrastructure`.
8.  `<ModuleName>Module.cs` is a composition entry point and must not
    become a large endpoint/business-logic file.
9.  `Presentation` must never directly access a module `DbContext`.
10. A module must never access another module's private
    persistence/entities.
11. Cross-module communication must use established Contracts/events.
12. Do not introduce repositories, MediatR, CQRS, generic `UnitOfWork`
    or similar abstractions unless an actual requirement/ADR justifies
    them.
13. Do not create empty architecture folders/classes for symmetry.
14. When modifying an older module that does not yet conform, do not
    silently perform a large unrelated refactor inside a product story.
    Follow the established architecture where practical and keep scope
    controlled.

## Frontend

-   Angular + TypeScript.
-   **PrimeNG is the primary UI component library. Use PrimeNG before
    building equivalent custom controls.**
-   Use PrimeIcons where suitable.
-   Do not add Angular Material or another broad UI library without an
    ADR.
-   Do not wrap every PrimeNG component; wrap only repeated Squad CRM
    design/business/accessibility behavior.
-   Preserve approved UX/design; PrimeNG is the implementation library,
    not a replacement design system.
-   Support Arabic/English, RTL/LTR and responsive desktop/tablet/mobile
    web.
-   Frontend authorization is UX only; backend is authoritative.

## List State, Query Parameters, and Input Security

Rules for all future frontend/backend stories:

1.  Server-backed list state must be URL-addressable where user-visible
    state includes search/filter/pagination/sort.
2.  Browser query parameters are the source of truth for list state.
3.  Refresh, deep links and browser Back/Forward must preserve list
    state.
4.  API requests must derive query parameters from validated/normalized
    URL state.
5.  Search/filter changes reset pagination unless explicitly specified
    otherwise.
6.  Use Angular `HttpParams` or established typed query construction;
    never unsafe query-string concatenation.
7.  Treat URL params and all client input as untrusted.
8.  Backend validation is authoritative.
9.  Validate ranges, lengths, IDs, enums, filters and sortable fields.
10. Do not globally strip special characters, apostrophes, Arabic,
    Unicode or HTML-looking characters as a security mechanism.
11. SQL injection protection relies on parameterized database access and
    allow-listed dynamic query structure.
12. Plain user text must be rendered through Angular's safe
    text/interpolation path.
13. Do not bypass Angular sanitization for untrusted content.
14. Rich HTML, if intentionally supported, requires a dedicated
    allow-list sanitizer.
15. Frontend validation is UX defense, not the security boundary.

Use `injectListUrlState` and the `list-query` readers in
`@squad-crm/platform` rather than re-deriving list state per screen, and
attach `ValidatesDataAnnotations<PaginationRequest>()` — plus the
endpoint's own `[AsParameters]` query record — to every list route.

## Security/quality

Authorization = Permission + Organizational Scope + Resource Ownership
where applicable. Never log passwords, tokens, OTPs or provider secrets.
Validate server-side. Add material tests, reproducible migrations and
architecture tests. Satisfy Definition of Done before completion.
