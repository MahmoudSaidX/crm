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

## Security/quality

Authorization = Permission + Organizational Scope + Resource Ownership
where applicable. Never log passwords, tokens, OTPs or provider secrets.
Validate server-side. Add material tests, reproducible migrations and
architecture tests. Satisfy Definition of Done before completion.
