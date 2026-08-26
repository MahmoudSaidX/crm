# Backend — Squad CRM

ASP.NET Core modular monolith foundation. The solution hosts technical building
blocks and independently-bounded modules behind explicit contracts, so a module
can later be extracted when — and only when — that is justified.

## Prerequisites

- **.NET SDK 10.0.111** (pinned in [`global.json`](global.json), `rollForward: latestFeature`).
- Target framework: **`net10.0`** for every project.

### SDK selection rationale

The version was selected from `dotnet --list-sdks` on the development machine
rather than assumed, and it is a supported LTS release that was **already
installed** — so CRM-105 required no SDK, workload or global-tool installation or
upgrade. `global.json`, every project's `TargetFramework` and this document are
kept in lockstep; changing one without the others is a defect.

```
$ dotnet --list-sdks
10.0.111 [/usr/lib/dotnet/sdk]
```

## Common commands

Run from `src/backend/`:

| Command | Purpose |
|---|---|
| `dotnet tool restore` | Restore the local tool manifest (`.config/dotnet-tools.json`), which pins `dotnet-ef`. Never install `dotnet-ef` globally — two developers would get different tool versions |
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the solution |
| `dotnet run --project src/Api/SquadCrm.Api` | Run the API host |
| `dotnet test` | Run architecture, API and persistence tests. The persistence suite **needs** `docker compose up -d` and the env values loaded — it fails, it does not skip |
| `dotnet test tests/SquadCrm.ArchitectureTests` | Static architecture rules. **No database needed** |
| `dotnet test tests/SquadCrm.Api.Tests` | API host tests. **No database needed** |
| `dotnet ef migrations add <Name> --project <module> --startup-project <module> --context <ModuleDbContext> --output-dir Persistence/Migrations` | Scaffold a migration into the owning module. Requires the env values (below); PostgreSQL need not be running |
| `dotnet ef database update --project <module> --startup-project <module> --context <ModuleDbContext>` | Apply that module's migrations. Requires the env values **and** a running server |

**Every `dotnet ef` command has one prerequisite:** the `POSTGRES_*` values must
be in the process environment. They are never read from a file by the
application — load them into the shell first, from `src/backend/`:

```bash
set -a && . ../../env/backend.env && set +a
```

```powershell
# PowerShell equivalent
Get-Content ../../env/backend.env |
  Where-Object { $_ -match '^\s*[^#].*=' } |
  ForEach-Object { $name, $value = $_ -split '=', 2; Set-Item "env:$name" $value }
```

Without them, `dotnet ef` fails fast naming the missing keys — and printing no
value. That failure is correct: the fix is to load the file, **never** to teach
the design-time factory to go looking for it.

## Local URLs

| URL | Notes |
|---|---|
| `http://localhost:5080/health` | Liveness probe. Returns `200 OK` with `{"status":"Healthy"}` |
| `http://localhost:5080/openapi/v1.json` | **Development only.** Built-in OpenAPI document |

There is deliberately **no Swagger UI, Scalar, NSwag or other OpenAPI UI**: this
foundation uses the built-in .NET OpenAPI support only, and consumers read the
JSON document. Adding an interactive UI is a separate decision for a later story.
The `v1` segment is the built-in document name — it is **not** an API-versioning
decision, which CRM-105 does not make.

See [`docs/api-conventions.md`](../../docs/api-conventions.md) for the shared
success/error/pagination/validation/security/versioning contract every module
endpoint follows (CRM-204).

## Layout

```
src/backend/
├── SquadCrm.sln
├── global.json
├── Directory.Build.props
├── .config/dotnet-tools.json                            (pins `dotnet-ef`)
├── src/
│   ├── Api/
│   │   └── SquadCrm.Api/                                (ASP.NET Core Web API host / composition root)
│   ├── BuildingBlocks/
│   │   └── SquadCrm.BuildingBlocks/                     (technical cross-cutting only; provider-neutral)
│   ├── Infrastructure/
│   │   └── SquadCrm.Infrastructure.Postgres/            (PostgreSQL configuration adapter; ADO Npgsql only, no EF Core)
│   └── Modules/
│       └── ArchitectureFixture/
│           ├── SquadCrm.Modules.ArchitectureFixture.Contracts/     (public contract surface)
│           └── SquadCrm.Modules.ArchitectureFixture/               (implementation)
│               └── Persistence/                                    (module-owned DbContext, entity, mapping)
│                   └── Migrations/                                 (this module's migrations only)
└── tests/
    ├── SquadCrm.ArchitectureTests/                      (xUnit + NetArchTest.Rules; static only)
    ├── SquadCrm.Api.Tests/                              (xUnit + WebApplicationFactory; no database)
    └── SquadCrm.Persistence.IntegrationTests/           (xUnit; requires real PostgreSQL)
```

### Allowed dependency directions

Enforced by `SquadCrm.ArchitectureTests`; a violation fails `dotnet test`.

- `SquadCrm.Api` → `BuildingBlocks`, `SquadCrm.Infrastructure.Postgres`, the
  module implementation and its contracts (composition root only) — but **never**
  a module's `*.Persistence` namespace, and it carries **no EF Core package**.
- A module implementation → `BuildingBlocks`, its own contracts and
  `SquadCrm.Infrastructure.Postgres`.
- `*.Contracts` → no project references at all, and **never** EF Core or Npgsql.
- `BuildingBlocks` → never a module, the API host, an `SquadCrm.Infrastructure.*`
  adapter, EF Core or Npgsql. It stays **provider-neutral**.
- `SquadCrm.Infrastructure.Postgres` → the ADO `Npgsql` package only. **Never**
  EF Core, never a module, never the host.
- **EF Core** → module implementation projects only.
- A module → never another module's implementation assembly (only its
  `*.Contracts`) and never another module's `*.Persistence` namespace.

## The `ArchitectureFixture` module

`SquadCrm.Modules.ArchitectureFixture` and its endpoint
`GET /internal/architecture-fixture/module-info` are **infrastructure-only
architecture scaffolding**, not a CRM capability. The name is deliberately
non-business. Their only purpose is to prove:

- explicit `IModule` registration by the host (no runtime assembly scanning);
- the contracts-vs-implementation split;
- module endpoint composition;
- the dependency rules above.

It additionally proves **module-owned persistence**: its own `DbContext`, its own
`architecture_fixture` schema, its own `persistence_probe` table, its own
migrations and its own migration-history table (see **Persistence** below). That
scaffolding is temporary on exactly the same terms as the rest of the fixture —
when it goes, its schema and migrations go with it.

The fixture **must not** grow into a business module, nor into a cross-cutting
"Platform" module — cross-cutting technical concerns belong in `BuildingBlocks`.
It **can and should be removed** once real business modules provide equivalent
architecture-rule coverage, at which point the architecture rules retarget to the
real module assemblies.

## Error contract

Every error response is **RFC 9457 Problem Details** (`application/problem+json`)
carrying `type`, `title`, `status`, `instance` and a lowercase `traceId`
extension. Stack traces, exception messages and inner-exception content are never
written to the response body in any environment; they go to the log, correlated
to the caller by `traceId`.

An inbound `X-Correlation-Id` is sanitised before use — a value that is empty,
longer than 128 characters, or that contains control characters is replaced by a
freshly generated identifier, and a client value is never echoed unsanitised.

## Validation

`SquadCrm.BuildingBlocks.Validation` provides a **minimal extension point** only:
an opt-in endpoint filter over the built-in
`System.ComponentModel.DataAnnotations.Validator` that renders failures as
`HttpValidationProblemDetails`, so validation failures share the shape of every
other error.

The **business-validation strategy is deliberately deferred** to the first
stories that introduce real requests and endpoints — where rules live, how they
compose, and whether any abstraction layer is warranted. CRM-105 adds no
validation library and invents no business rules.

## Persistence

PostgreSQL is the system database (`docs/adr/ADR-002-postgresql.md`). SQL Server
is not used.

### One `DbContext` per module

Each module owns **its own** `DbContext`, inside its own implementation project,
under a `Persistence/` folder. There is deliberately **no** shared
`SquadCrmDbContext`: a shared context would make every module's model a shared
compile-time and migration-time dependency, which is exactly the coupling the
modular monolith exists to prevent. The architecture rule
`EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace` fails the build if
a central context reappears, or if a context is parked in the host, in
`BuildingBlocks` or in the infrastructure adapter.

### Schema ownership

Each module owns one PostgreSQL schema and every table in it. A module may not
query or mutate another module's tables — not by EF mapping, not by raw SQL and
not through a view. Cross-module workflows use public contracts and events. The
`ArchitectureFixture` module owns the `architecture_fixture` schema; the name is
deliberately unmistakable as non-business.

No module table is ever placed in the PostgreSQL `public` schema. There are no
cross-schema foreign keys.

### Where PostgreSQL configuration lives — and why not in `BuildingBlocks`

Reading the `POSTGRES_*` values, validating them, assembling the connection
string and rendering redacted diagnostics exist in **exactly one** place:
`src/Infrastructure/SquadCrm.Infrastructure.Postgres`. It is a provider-specific
adapter at the composition/infrastructure boundary — configuration only. It holds
no `DbContext`, no entity, no repository and no migration, and it references the
**ADO `Npgsql` package only, never EF Core**.

It is not in `BuildingBlocks` because `BuildingBlocks` is provider-neutral by
policy (`CLAUDE.md`): external providers stay behind provider-neutral ports, and
PostgreSQL is one such provider. It is not in `SquadCrm.Api` either, because a
module's design-time factory must reach the same implementation and a module may
never reference the host. Both facts are enforced —
`BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` and
`InfrastructurePostgres_MustNotDependOnEfCoreModulesOrApi`.

### `POSTGRES_*` → `ConnectionStrings:SquadCrmPostgres`

`POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER` and
`POSTGRES_PASSWORD` (owned by CRM-197, in `env/backend.env`) are the **only**
database configuration an operator ever sets. At composition time the host calls
`builder.AddSquadCrmPostgres()`, which reads them once, validates them fail-fast
and publishes the derived connection string **internally** as
`ConnectionStrings:SquadCrmPostgres`. Modules obtain it through the
`IConfiguration` they already receive, via
`configuration.GetSquadCrmPostgresConnectionString()`.

That name is application-internal. It is never read from a file or the
environment, never documented as a setting, and there is no `ConnectionStrings`
section in `appsettings.json`.

Missing or invalid configuration throws **one** exception listing **every**
offending key by name. No value is ever echoed — not the port (it may carry
pasted junk) and never the password or the assembled connection string. The only
permitted rendering for logs, exceptions and test output is `Describe()`, which
omits the password entirely.

### Runtime and design time share one implementation

The API composition root and each module's `IDesignTimeDbContextFactory` call the
same `ReadPostgresOptions()` / `BuildConnectionString()`, and each module's
provider options (including the migrations-history placement) are applied by one
internal helper used by both paths. `dotnet ef` and the running application can
therefore never disagree about what is required or how it is assembled.

The design-time factory reads **the process environment only**. It never locates
or parses `env/backend.env`: loading that file into the shell is the documented
developer step above, not application behaviour.

### Migration history

Each module's history table lives **inside that module's own schema** —
`architecture_fixture.__ef_migrations_history` for the fixture. Several module
contexts share one physical database, so a shared `public.__EFMigrationsHistory`
would let `dotnet ef database update` for one module mark another module's
migrations as applied. A module that forgets `MigrationsHistoryTable(...)` writes
to `public` instead; the integration suite's `PublicSchema_HoldsNoSquadCrmTables`
is what catches that.

### Applying migrations

Migrations are applied by an **explicit** command, never at startup. `Program.cs`
contains no `Database.Migrate()`: the application must not silently mutate a
database when it boots.

```bash
# 1. Infrastructure up (from the repository root)
export COMPOSE_ENV_FILES=env/backend.env && docker compose up -d

# 2. Load the operator values into the shell (from src/backend/)
set -a && . ../../env/backend.env && set +a

# 3. Tools, then apply
dotnet tool restore
dotnet ef database update \
  --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --context ArchitectureFixtureDbContext

# 4. Confirm
dotnet ef migrations list \
  --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --context ArchitectureFixtureDbContext
```

Rolling one module's schema back: `dotnet ef database update 0 --project … --context …`.

### Resetting the local database

From the repository root, `docker compose down -v` **destroys the
`squadcrm-pgdata` volume and every local row**, then `docker compose up -d`
followed by step 3 above recreates everything **from migrations alone** — no init
SQL, no manual `CREATE SCHEMA`. That is also the remedy for two specific
symptoms: a migration that failed half-way (history and schema now disagree), and
an authentication failure with correct-looking configuration (PostgreSQL honours
`POSTGRES_USER`/`PASSWORD`/`DB` only on an empty data directory).

Concurrent `dotnet ef database update` runs are a developer-workflow hazard, not
something automated here: EF takes a lock, so the second run waits or fails.

### Adding persistence to a new module

Inside that module's implementation project, and nowhere else:

1. A schema constant class naming the module's schema, its probe/business tables
   and its `__ef_migrations_history` table.
2. The entity types, plus one `IEntityTypeConfiguration` each with explicit
   lowercase `snake_case` `ToTable`/`HasColumnName` mappings.
3. The module's `DbContext`, calling `HasDefaultSchema(<its own schema>)` and
   applying its configurations. It maps **only** this module's model.
4. An internal options helper applying `UseNpgsql(...)` with
   `MigrationsHistoryTable(<history table>, <its own schema>)`.
5. An `IDesignTimeDbContextFactory` reading the process environment and calling
   the shared `SquadCrm.Infrastructure.Postgres` implementation — **never** a
   second parser, validator or connection-string builder.
6. `services.AddDbContext<TContext>(...)` in that module's `RegisterServices`,
   using `configuration.GetSquadCrmPostgresConnectionString()`. The host does not
   register it.
7. `dotnet ef migrations add … --output-dir Persistence/Migrations` against that
   module, and commit the migration **and** the `*ModelSnapshot.cs`.

Do **not** touch `BuildingBlocks`, do not add an EF Core package to the host or
the adapter, and do not hand-edit generated migration SQL to patch a modelling
mistake — fix the model, delete the migration and regenerate.

### EF Core and Npgsql versions

| Package | Version | Where |
|---|---|---|
| `Npgsql` (ADO) | `10.0.3` | `SquadCrm.Infrastructure.Postgres` only |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` | module implementation projects only |
| `Microsoft.EntityFrameworkCore.Design` | `10.0.4` | module implementation projects, `PrivateAssets=all` |
| `dotnet-ef` | `10.0.4` | `.config/dotnet-tools.json` |

All are explicit, pinned, stable `10.*` — no floating versions, no `*`, no
previews (`Npgsql` 11.x is preview only). The whole EF Core family sits on
**`10.0.4`**, the version the official Npgsql provider `10.0.3` resolves, and the
ADO `Npgsql` package matches the version the provider resolves so the two cannot
diverge. A higher `Design` version silently lifts EF Core inside the module but
not inside `SquadCrm.ArchitectureTests` — `PrivateAssets` hides `Design` from it —
and the two then fail assembly unification at build time. That is why `Design`
and `dotnet-ef` are held at `10.0.4` rather than tracking the newest patch.

Deliberately absent: `EFCore.NamingConventions` (three explicitly mapped columns
are not worth a solution-wide dependency), `EFCore.BulkExtensions`,
`Testcontainers` and `Respawn`.

### What the tests prove

`SquadCrm.ArchitectureTests` is **purely static**. It asserts assembly and
namespace dependency direction and `DbContext` placement, over IL and over
assembly references. It constructs no `DbContext`, invokes no design-time
factory, inspects no EF model and carries no EF Core package reference — it
recognises a context by full base-type name. It therefore **cannot** prove which
SQL runs at runtime.

`SquadCrm.Persistence.IntegrationTests` owns every claim about real database
state: the connection opens, migrations apply, no migration remains pending, the
schema exists, the table and its `snake_case` columns exist in that schema, the
module's history table exists and holds rows, `public` holds no table at all, and
a row round-trips through the module's own context and transaction. It requires
the CRM-197 PostgreSQL service and **fails loudly** when it is absent — it never
skips, because a green run must mean the database was really exercised.

So the full `dotnet test` **requires** `docker compose up -d`;
`dotnet test tests/SquadCrm.Api.Tests` and
`dotnet test tests/SquadCrm.ArchitectureTests` run without a database. **CI
orchestration and test filtering are CRM-202's**, not designed here.

A module issuing raw SQL against another module's schema remains a **coding
convention**: nothing above can catch it. Enforcing it at the database level
would need per-module PostgreSQL roles, which is deliberately out of scope here.

## Non-goals in this foundation

Owned by later stories and intentionally absent here — the absence is enforced by
architecture rule `Foundation_MustNotIntroduceForbiddenDependencies`, which those
stories must update when they legitimately introduce a dependency:

| Not here | Owning story |
|---|---|
| Integration events / transactional outbox | CRM-198 |
| File storage adapters | CRM-200 |
| OpenTelemetry observability pipeline | CRM-201 |
| Broader testing infrastructure | CRM-202 |
| Authentication / session implementation (extension point ready) | CRM-110 |
| External integrations | CRM-192 |
| Hangfire, HTTPS/deployment, API versioning | later stories |

**CRM-106 deliberately does not solve:** cross-module distributed transactions;
the transactional outbox and integration events (CRM-198); per-module PostgreSQL
roles or table-level permissions; production migration automation; migration on
application startup; database readiness in `/health` (CRM-201); CI test
orchestration and filtering (CRM-202).

`/health` is a **liveness** probe only. It performs no database, storage or
provider checks.

## Warnings-as-errors

`TreatWarningsAsErrors=true` is set in `Directory.Build.props` and must **not** be
globally disabled, replaced wholesale by `WarningsNotAsErrors`, or worked around
with a `Directory.Build.props`-level `NoWarn` to make a build pass. Fix the cause
first; if the cause is genuinely outside our control, apply the narrowest possible
suppression (a `#pragma warning disable`/`restore` around the exact lines, or
`NoWarn` on the single affected project), comment it inline with what and why, and
record it here.

**Suppressions currently in place — one.** `.editorconfig` relaxes exactly one
style rule, `csharp_style_namespace_declarations`, for generated migration files
only (`[**/Persistence/Migrations/*.cs]`). EF Core emits block-scoped namespaces
and regenerates those files wholesale on every `dotnet ef migrations add`, so
hand-reformatting them would be undone by the next scaffold. No analyser is
disabled, no other rule is relaxed, and no other path is covered. Every
hand-written file still builds clean with zero warnings.
