# ADR-012 --- Internal Module Layering

**Status:** Accepted.

## Context

`ADR-001` fixes the architecture: a modular monolith whose business
modules are the primary boundary. It says nothing about how a module
organises its own code, and modules had grown flat --- HTTP DTOs,
application services, permission constants and every endpoint handler at
the project root, with business entities sharing the `Persistence`
namespace with the `DbContext`, EF configurations and the outbox. That
left no boundary a rule could name, so nothing could state "a domain
entity must not depend on EF Core" or "an endpoint must not open a
`DbContext`".

## Decision

Every business module organises its internals by responsibility:

| Layer | Owns |
| -- | -- |
| `Domain` | entities, aggregates, value objects, domain behaviour, domain and integration events, pure business policies |
| `Application` | application services, use-case orchestration, workflow coordination |
| `Presentation` | endpoint definitions, routes, request/response DTOs, HTTP status mapping, HTTP authorization attachment |
| `Infrastructure` | `DbContext`, EF configurations, migrations, outbox persistence and interceptors, Hangfire jobs, authentication/authorization adapters, demo-data contributors, technical adapters |

`<ModuleName>Module.cs` and `Permissions.cs` stay at the module root.
`<ModuleName>Module.cs` is a composition entry point: it registers
services and infrastructure and delegates endpoint mapping.

Namespaces follow folders, which is what makes the rules enforceable.

Enforced directions (`SquadCrm.ArchitectureTests`):

- `Domain` must not depend on `Presentation`, `Application` or
  `Infrastructure`.
- `Domain` must not depend on EF Core, Npgsql, Hangfire or ASP.NET Core.
- `Presentation` must not depend on any `DbContext`.
- The module composition root must not declare HTTP handlers.
- A module must not depend on another module's implementation or
  persistence namespace (pre-existing rules, unchanged).

`Application` --> `Presentation` is deliberately allowed: request and
response records are the module's HTTP vocabulary and its services take
them directly. A parallel set of mapper types would be ceremony, not a
boundary.

## What this is not

- **Not** a change to the architecture. The modular monolith, module
  ownership, `*.Contracts` projects and events are unchanged; module
  boundaries remain primary and these layers are internal to one module.
- **Not** project-per-layer Clean Architecture. One project per module,
  four folders inside it. No layer is separately deployable and none
  implies independent deployment.
- **Not** a mandate for repositories, MediatR, CQRS, generic
  `UnitOfWork` or any new architectural package. Pragmatic EF Core use
  inside an application service remains the pattern.
- **Not** a reason to create empty folders or placeholder types. A module
  owns a layer folder only when it has code for it.

Agent, API and infrastructure code must respect module ownership: the
host composes modules and never reaches into a module's Domain or
Infrastructure.

## Rule

Story plans may refine details but must not silently contradict this
ADR. New backend code is placed by responsibility; an older module that
does not yet conform is brought along only as far as the story it is
part of actually reaches.
