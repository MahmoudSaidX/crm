# Architecture

Squad CRM starts as a modular monolith with strong business boundaries,
explicit contracts and independent persistence ownership so modules can
be extracted later with less redesign.

ASP.NET Core hosts modules. PostgreSQL/EF Core uses schema-per-module.
Cross-module synchronous calls use explicit application contracts only
when immediate consistency is required; durable side effects favor
integration events + transactional outbox.

Angular provides Agent CRM and Customer Portal surfaces with reusable
shared libraries and business-capability boundaries. PrimeNG is the
primary UI library and is adapted to the Squad CRM design system.

ERP, AI, Email, WhatsApp, SMS and storage use ports/adapters. Reporting
uses dedicated read models rather than cross-module transactional joins.
Local development should avoid paid dependencies where possible.

## Current module boundaries

- `SquadCrm.Api` is the composition root. It registers modules and cross-cutting infrastructure but does not access module persistence internals.
- `SquadCrm.BuildingBlocks.Abstractions` contains dependency-free contracts; `SquadCrm.BuildingBlocks` contains provider-neutral technical concerns.
- Each `SquadCrm.Modules.<Name>` implementation owns its domain and persistence. Other modules may reference only its `.Contracts` project.
- `SquadCrm.Infrastructure.*` projects are provider adapters and do not contain business state.
- ArchitectureFixture is removable scaffolding, not a CRM product module. Its development seed is synthetic and module-owned.

These boundaries are enforced by `SquadCrm.ArchitectureTests`; persistence behavior is verified against PostgreSQL by `SquadCrm.Persistence.IntegrationTests`.
