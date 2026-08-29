# Squad CRM --- Codex Instructions

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

## Story Execution Safety

- Never select or start a story unless the user explicitly names or approves it.
- Linear is the product/work-item source of truth. The matching approved Squad
  Kit plan is the implementation-plan source of truth. Existing ADRs govern
  cross-cutting architectural decisions. Code is the current implementation
  reality. Surface conflicts instead of silently resolving them.
- Before implementation, reconcile only the active story against its Linear
  issue and blockers; git status/history and existing PR evidence; its matching
  Squad Kit intake/plan; and existing implementation relevant to that story.
- Reuse an existing approved plan. Never regenerate an approved intake or plan
  unless explicitly requested.
- Apply YAGNI: do not introduce a new abstraction, framework, provider
  interface, extension point, registry, configuration mechanism or
  infrastructure unless justified by the current story, a current consumer, an
  existing ADR or strong evidence that deferring it creates a significant
  breaking change.
- Implement directly using established repository patterns. Do not solve
  downstream stories.
- Stop for user input only for a genuine product/scope decision, architectural
  boundary/contract decision, security or data-integrity decision, or
  source-of-truth conflict. Resolve ordinary implementation choices
  autonomously.
- Use targeted reads and tests during implementation. At completion, run the
  verification required by the story/plan and review the final diff. Do not
  repeatedly rerun unchanged reviews or reload unrelated ADRs.
- Never commit, push, merge, open a PR, modify Linear or perform other
  publication actions without explicit user approval.
- After completing one story, stop. Never automatically start the next story.

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
