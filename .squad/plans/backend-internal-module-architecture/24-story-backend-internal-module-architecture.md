# Plan — Backend Internal Module Architecture

Structural refactor of existing code. The Modular Monolith, its module
boundaries, its `*.Contracts` projects and its events stay exactly as they are;
only the *inside* of each business module is reorganised by responsibility.
The diff is dominated by `git mv`, namespace and `using` changes, plus one
mechanical extraction of endpoint handlers out of `<Module>.cs`.

**Behaviour freeze is the governing constraint.** If a move would require a
behaviour change, the move is abandoned and the exception documented. No
migration is generated.

ADR: `docs/adr/ADR-012-internal-module-layering.md` (new).

## Module classification (after inspecting `src/backend/src/Modules`)

| Module | Class | Treatment |
| -- | -- | -- |
| AgentTaskManagement | business | full layering |
| Audit | business (cross-cutting, has entity + endpoints) | full layering |
| BranchManagement | business | full layering |
| BrandingManagement | business | full layering |
| CustomerManagement | business | full layering |
| DepartmentManagement | business | full layering |
| QuickReplyManagement | business | full layering |
| RoleManagement | business | full layering |
| StaffIdentity | business | full layering |
| SystemConfiguration | business | full layering |
| TicketManagement | business | full layering |
| ArchitectureFixture | fixture/testing | **not** business-layered; only `Persistence/` + `BackgroundProcessing/` move under `Infrastructure/` so the persistence-namespace rule stays uniform |
| `*.Contracts` projects | cross-module public boundary | untouched |
| `StaffIdentity.Bootstrap`, `Tools/*` | special-purpose executables | untouched |
| `BuildingBlocks*`, `Infrastructure.*` | foundation | untouched |

## Migration map (by responsibility, applied to every business module)

| Current | Responsibility | Target | Reason |
| -- | -- | -- | -- |
| `Persistence/<Entity>.cs` (Customer, Ticket, QuickReply, Branch, Department, Role, StaffUser, AgentTask, AuditRecord, BrandingAsset, ConfigurationValue, …) | business entity/aggregate | `Domain/Entities/` | entity identity is domain, not storage |
| `Persistence/TicketPriority.cs`, `TicketCategory.cs`, `*History.cs`, `*AuditEvent.cs`, `RolePermission.cs`, `PermissionDefinition.cs`, `StaffSubjectRole.cs`, `RefreshSession.cs`, `AuthenticationEvent.cs`, `TicketWatcher.cs`, `TicketInternalNote.cs`, `CustomerContact/Note/Attachment.cs` | business entity | `Domain/Entities/` | same |
| `Persistence/TicketStatusTransitions.cs` | pure business policy | `Domain/Policies/` | fixed lifecycle matrix, no I/O |
| `ThemeTokenCatalog.cs`, `ConfigurationCatalog.cs` | pure business policy/catalog | `Domain/Policies/` | allow-lists evaluated in-memory |
| `Events/*DomainEvent.cs`, `Events/*IntegrationEvent.cs` | event definitions | `Domain/Events/` | event vocabulary of the module |
| `Persistence/<Module>DbContext.cs`, `…DbContextFactory.cs`, `…Schema.cs`, `…DbContextOptions.cs`, `*Configuration.cs` | EF Core technical implementation | `Infrastructure/Persistence/` | ORM/storage concern |
| `Persistence/Migrations/**` | migrations | `Infrastructure/Persistence/Migrations/` | unchanged migration ids |
| `Persistence/OutboxMessage.cs`, `OutboxMessageConfiguration.cs`, `*OutboxInterceptor.cs` | outbox persistence | `Infrastructure/Outbox/` | durable-delivery plumbing, not business state |
| `BackgroundProcessing/*Job.cs` and outbox dispatcher/store/health/telemetry | Hangfire + outbox execution | `Infrastructure/BackgroundProcessing/` | execution infrastructure |
| `DemoData/*` | dev seeding adapter | `Infrastructure/DemoData/` | technical adapter for the seeder tool |
| `HttpCurrentUserAccessor.cs`, `AuthenticationOptions.cs`, `StaffIdentityModule.ValidateActiveSessionAsync` | authn plumbing | `Infrastructure/Authentication/` | ASP.NET Core / JWT technical adapter; also removes the only Presentation→DbContext access |
| `PermissionAuthorization.cs`, `GlobalQuickReplyAuthorizer.cs` | ASP.NET Core authorization handler / policy probe | `Infrastructure/Authorization/` | framework adapter |
| `<X>Service.cs` (all), `AuditRecorder.cs`, `AuditQueryService.cs`, `AuthorizationBootstrapService.cs`, `AgentTaskReminderService.cs` | use-case orchestration | `Application/Services/` | reminder *workflow* is Application even though its Hangfire job is Infrastructure |
| `*ActiveLookup.cs`, `CustomerExistsLookup.cs`, `TicketExistsLookup.cs`, `StaffSubjectReferenceReader.cs` | implementations of cross-module contracts | `Application/Services/` | use-case reads serving another module's contract |
| `<X>Contracts.cs` (HTTP DTOs) | HTTP request/response DTOs | `Presentation/Requests/` + `Presentation/Responses/` | HTTP shape |
| endpoint handlers currently inside `<Module>.cs` | HTTP routing + status mapping | `Presentation/Endpoints/<Area>Endpoints.cs` | composition entry point must stay small |
| `Permissions.cs`, `<Module>Module.cs` | module composition | module root (unchanged) | per the target structure |

Namespaces follow folders exactly: `SquadCrm.Modules.X.Domain.Entities`,
`…Domain.Events`, `…Domain.Policies`, `…Application.Services`,
`…Presentation.Requests`, `…Presentation.Responses`, `…Presentation.Endpoints`,
`…Infrastructure.Persistence`, `…Infrastructure.Persistence.Migrations`,
`…Infrastructure.Outbox`, `…Infrastructure.BackgroundProcessing`,
`…Infrastructure.DemoData`, `…Infrastructure.Authentication`,
`…Infrastructure.Authorization`. `<Module>Module.cs` and `Permissions.cs` keep
`SquadCrm.Modules.X`. Each `<X>Contracts.cs` splits into
`Presentation/Requests/<X>Requests.cs` and
`Presentation/Responses/<X>Responses.cs`.

Layer classification of new code for this story: **all four layers plus none
of the cross-module Contracts** — no contract, event, outbox, background-job or
persistence *behaviour* is added; endpoint ownership, domain ownership and
persistence ownership are unchanged per module. **Migration expected: none.**

## Endpoint extraction

`<Module>.cs` keeps `Name`, `RegisterServices`, and a `MapEndpoints` that calls
`XEndpoints.Map(endpoints)`. Handlers, `Problem(...)` mapping and `ToResponse`
projections move verbatim into `Presentation/Endpoints/`. Grouped per cohesive
area, not per endpoint: TicketManagement splits into `TicketEndpoints` (tickets
plus their notes/watchers sub-resources), `TicketCategoryEndpoints` and
`TicketPriorityEndpoints`; CustomerManagement into `CustomerEndpoints` (which
also carries the timeline route — a single handler is not its own file) plus
`CustomerContactEndpoints`, `CustomerNoteEndpoints` and
`CustomerAttachmentEndpoints`, each mapping onto the `customers` route group it
is handed. Every other module gets one `<Module>Endpoints.cs`.

Two visibility widenings are required and are the only non-move edits in the
extraction: `CustomerEndpoints.NotFoundProblem` becomes `internal` (the contact,
note and attachment files share it), and StaffIdentity's
`ValidateActiveSessionAsync` becomes `public` on the new
`Infrastructure/Authentication/RefreshSessionValidator`.

`internal`/`private static` accessibility is preserved where the moved members
stay inside the same assembly.

## Architecture tests (extend `tests/SquadCrm.ArchitectureTests`, existing style)

New `InternalModuleLayeringRulesTests.cs`, asserted over the module assemblies
already listed in `SquadCrmAssemblies`:

- `Domain_MustNotDependOnPresentation` — `…X.Domain` ✕→ `…X.Presentation`.
- `Domain_MustNotDependOnInfrastructureOrEfCore` — `…X.Domain` ✕→
  `…X.Infrastructure`, `Microsoft.EntityFrameworkCore`, `Npgsql`, `Hangfire`,
  `Microsoft.AspNetCore`.
- `Presentation_MustNotDependOnModuleDbContext` — no type under
  `…X.Presentation` may reference a `DbContext`-derived type.
- `ModuleCompositionRoot_MustNotDeclareHttpHandlers` — no `*Module` type in the
  module root namespace may declare a method returning `IResult`/`Task<IResult>`.
  A file-length guard is deliberately *not* used: it would be arbitrary and
  easy to satisfy without fixing anything.

Each rule is a `[Theory]` over every module implementation assembly, so a new
module is covered the moment it joins `SquadCrmAssemblies`. `SquadCrmAssemblies`
and `SquadCrm.ArchitectureTests.csproj` gain the six module assemblies that were
previously only reachable transitively (Branch, Branding, Customer, Department,
SystemConfiguration, TicketManagement), which also widens the pre-existing
cross-module rules to cover them.

Both new directions are proven non-vacuous by temporarily introducing a
violation (a `typeof(DbContext)` in a Domain entity; a `DbContext` reference in
an endpoints class) and confirming the suite fails.

Existing rules are kept; `SquadCrmAssemblies.PersistenceNamespaceSuffix`
becomes `.Infrastructure.Persistence` so
`EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace` and
`Modules_MustNotDependOnAnotherModulesPersistenceNamespace` keep working
against the new location.

## Documentation / rules updates

- `docs/adr/ADR-012-internal-module-layering.md` — new, concise, in the house
  ADR style.
- `CLAUDE.md` + `AGENTS.md` — new `## Backend Module Internal Architecture`
  section (14 short rules). No ADR text duplicated.
- `scripts/migrate`, `scripts/seed`, `src/backend/README.md` — the migrations
  and seed paths move under `Infrastructure/`. `src/backend/.editorconfig`'s
  `[**/Persistence/Migrations/*.cs]` glob still matches and needs no change.
- `docs/development/implementation-workflow.md` — Squad Kit planning rule: every
  backend plan classifies new code as Domain / Application / Presentation /
  Infrastructure / cross-module Contract, and states endpoint, domain and
  persistence ownership, cross-module dependencies, event/outbox involvement,
  background-processing involvement and whether a migration is expected.
- `docs/architecture/architecture.md` — pointer to the ADR.

## Verification

- `dotnet build` (warnings are errors).
- `dotnet test` — UnitTests, Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests.
- `dotnet format --verify-no-changes`.
- `dotnet ef migrations has-pending-model-changes` per module context (or the
  repository's `scripts/migrate` equivalent) — must report none.
- `git diff -M -C --stat` reviewed for rename detection; any hunk that is not a
  move/namespace/using/extraction change is investigated before commit.
