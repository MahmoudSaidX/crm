# Backend Internal Module Architecture

Platform/architecture story. No Linear product issue: this is a structural
refactor of code the repository already owns, not new business capability.

## Story

As an engineer working across Squad CRM backend modules, I want every business
module to organise its own internals by responsibility — Domain, Application,
Presentation, Infrastructure — so that endpoint, use-case, business and
technical code are findable and separable without changing the Modular
Monolith's module boundaries.

## Problem (current reality, after inspection)

Business modules are currently flat. A module directory mixes, at its root,
HTTP DTOs (`*Contracts.cs`), application services (`*Service.cs`), permission
constants and a `<Module>.cs` that carries every endpoint handler, HTTP status
mapping and response projection. `Persistence/` holds business entities
alongside the `DbContext`, EF configurations, outbox tables and interceptors,
so "entity" and "storage technology" share one namespace.

Consequences visible today:

- `TicketManagementModule.cs` is 590 lines and `CustomerManagementModule.cs`
  392 — composition, routing, HTTP mapping and DTO projection in one file.
- The only rule protecting persistence is `*.Persistence` namespace ownership.
  Nothing distinguishes a domain entity from a DbContext, so no rule can say
  "Domain must not depend on EF Core" or "Presentation must not touch a
  DbContext".
- `StaffIdentityModule.cs` resolves `StaffIdentityDbContext` directly inside a
  JWT `TokenValidated` handler, in the same file that maps endpoints.

## Acceptance Criteria

- Every business module organises its code as Domain / Application /
  Presentation / Infrastructure, with folders created only where a
  responsibility actually has code.
- `<ModuleName>Module.cs` registers services and infrastructure and delegates
  endpoint mapping; it carries no endpoint handler bodies.
- Endpoint definitions, HTTP request/response DTOs and HTTP status mapping live
  under `Presentation/`.
- Business entities, enums, domain behaviour, domain events and integration
  events live under `Domain/`.
- Application services and use-case orchestration live under `Application/`.
- `DbContext`, EF configurations, migrations, outbox persistence/interceptors,
  Hangfire jobs, demo-data contributors and authentication plumbing live under
  `Infrastructure/`.
- Architecture tests fail the build on: Domain → Presentation, Domain →
  Infrastructure, Presentation → module DbContext, module → another module's
  implementation or persistence.
- An ADR records that this is internal organisation inside an unchanged
  Modular Monolith, not project-per-layer Clean Architecture.
- Repository agent rules (`CLAUDE.md`, `AGENTS.md`) and the Squad Kit planning
  guidance state where future backend code belongs.

## Business Rules

- **Behaviour freeze.** No route, verb, query parameter, request/response JSON,
  status code, permission, validation rule, event name, event payload, outbox
  or Hangfire behaviour changes. No schema change and no new migration.
- Module boundaries remain the primary architecture boundary; cross-module
  communication continues through `*.Contracts` projects and events.
- No repositories, MediatR, CQRS framework, generic UnitOfWork or new
  architectural package is introduced.
- Infrastructure/foundation and fixture modules are not forced into
  business-module conventions.
- No empty folder or placeholder type is created for symmetry with the diagram.

## Out of scope

- Frontend, API host composition, and any product behaviour.
- Splitting modules into separate per-layer projects.
- Introducing per-module database roles or changing persistence technology.
