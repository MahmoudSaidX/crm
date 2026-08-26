# Story 05 — PostgreSQL + EF Core + Schema-per-Module (Story: CRM-106)

## Prerequisites

- **Story 03 completed (CRM-105 — ASP.NET Core Modular Monolith Foundation):** the solution, `IModule` composition, `Directory.Build.props` (`TreatWarningsAsErrors=true`), the architecture tests and the `ArchitectureFixture` module all exist. See [`../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md`](../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md). Follow its **"inspect the installed/published versions, never assume"** pattern — it applies here to every EF Core / Npgsql package.
- **Story 04 completed (CRM-197 — Docker Compose & Local Infrastructure):** root `docker-compose.yml` publishes `postgres:18.6-alpine3.24` on `127.0.0.1:${POSTGRES_PORT:-5432}` with the named volume `squadcrm-pgdata`. See [`../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md`](../crm-197-docker-compose-local-infrastructure/04-story-crm-197-docker-compose-local-infrastructure.md). **Do not change the Compose file, the image tag, the volume layout or the loopback publishing.**
- **`docs/adr/ADR-002-postgresql.md` is a binding read-only input** — "PostgreSQL + EF Core with schema-per-module. Modules own migrations and transactions." This story implements it; **do not amend any ADR.**
- **The `POSTGRES_*` keys in `env/backend.env.example` (lines 13–17) are the single operator-facing database configuration contract, owned by CRM-197.** This story consumes them. **Do not rename them and do not introduce a second externally configured set of database values.**
- Local PostgreSQL must be running for the integration verification: `docker compose up -d` from the repository root.
- Coordinate with the owners of the stories this one unblocks — CRM-198 (outbox/events), CRM-199 (Hangfire), CRM-202 (test infrastructure and CI orchestration), CRM-110/CRM-122/CRM-158 (first business modules). **Implement none of them here.**

---

## Story Goal

Give the backend a working, migration-driven PostgreSQL persistence foundation in which **each module owns its own `DbContext`, its own PostgreSQL schema, its own tables and its own migration history**, so the first real business module can add persistence without inventing the pattern.

1. The backend opens a PostgreSQL connection built from the **existing** `POSTGRES_*` environment contract, and fails fast with an actionable, secret-free error when that configuration is missing or invalid.
2. The `ArchitectureFixture` module carries the **minimum, explicitly non-business** persistence fixture — one `DbContext`, one schema, one table, its own migrations — proving module-owned persistence end to end.
3. Migrations are applied by an **explicit developer command**, never by application startup.
4. A clean database (`docker compose down -v` → `up -d`) can be fully recreated from migrations alone.
5. Architecture tests fail the build when persistence boundaries are crossed, and **`SquadCrm.BuildingBlocks` stays provider-neutral** — no EF Core, no Npgsql, no PostgreSQL-specific implementation detail.

**Explicitly out of scope:** real CRM entities/modules; a shared application-wide `DbContext`; cross-module direct database access or cross-module foreign keys; PostgreSQL role/permission isolation per module; `Database.Migrate()` on normal API startup; production migration automation; distributed transactions; the transactional outbox and domain/integration events (CRM-198); Hangfire (CRM-199); authentication/session persistence (CRM-110); the full test infrastructure and CI orchestration/filtering (CRM-202); database readiness in `/health` and the observability pipeline (CRM-201); file storage and external integrations; any frontend change.

---

## Context — Read These Files First

1. `.squad/stories/crm-106-postgresql-ef-core-schema-per-module/crm-106-postgresql-ef-core-schema-per-module/intake.md` — the full intake. The **Persistence architecture direction**, **Migration history**, **Architecture enforcement**, **Security / secrets** and **Out of scope** sections are hard constraints, not suggestions.
2. `src/backend/src/Api/SquadCrm.Api/Program.cs` — **lines 9–16** (composition-root comment and the documented configuration precedence), **lines 24–34** (the existing fail-fast configuration pattern: throw naming the *key*, never the value — the database validation must read the same way), **lines 54–55** (`AddHealthChecks()`, liveness only — **do not extend it**), **lines 57–64** (the explicit `IModule[]` list and `RegisterModules`, which passes `builder.Configuration` to every module).
3. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Modules/IModule.cs` — **lines 12–23**. `RegisterServices(IServiceCollection, IConfiguration)` is the **only** seam a module uses to obtain its connection string and register its own `DbContext`.
4. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/SquadCrm.BuildingBlocks.csproj` — **lines 7–13**. A `FrameworkReference` and nothing else. **This file must not change in this story.**
5. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/ArchitectureFixtureModule.cs` — **lines 11–45**. Note the class-level doc ("**not a CRM module**", "can and should be deleted") and `RegisterServices` at **lines 37–45**; the persistence registration is added there, and the doc comment must be extended in the same tone.
6. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.csproj` — **lines 7–14**. The EF Core packages are added **here**, in a module implementation project.
7. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/SquadCrm.Modules.ArchitectureFixture.Contracts.csproj` — **lines 7–8**: "Public contract surface. Intentionally has NO project references." **It must gain no package reference either.**
8. `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — **lines 74–91** (`ForbiddenAssemblyPrefixes`, whose comment names CRM-106 as the story that must update it), **lines 93–145** (existing rule style), **lines 152–193** (the generic module rule and the scope-creep guard), **lines 56–71** (the class doc that states these rules are deliberately *structural* only). New persistence rules must match this style: assert twice — NetArchTest over IL **and** over assembly references — and stay **purely static**.
9. `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` — **lines 12–49**. `All`, `IsModuleImplementation`, `ReferencedAssemblyNames` are the helpers the new rules reuse; `All` must gain the new infrastructure assembly.
10. `src/backend/tests/SquadCrm.Api.Tests/SquadCrmApiFactory.cs` — **lines 269–287** (the doc explaining the factory hosts the real configuration) and **lines 313–324** (`ConfigureWebHost`). Because API startup now validates database configuration, this factory must supply placeholder `POSTGRES_*` values; see Task 2.
11. `src/backend/tests/SquadCrm.Api.Tests/SquadCrm.Api.Tests.csproj` — the test-project shape (xUnit 2.9.3, `Microsoft.NET.Test.Sdk` 17.14.1, `coverlet.collector` 6.0.4, `<Using Include="Xunit" />`) that the new integration-test project mirrors.
12. `env/backend.env.example` — **lines 8–17**. The exact `POSTGRES_HOST`, `POSTGRES_PORT`, `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` names to consume. Note **line 27** shows the existing `CORS__AllowedOrigins__0` double-underscore style — the `POSTGRES_*` keys deliberately do **not** use it, which drives the mapping decision in Task 2.
13. `docker-compose.yml` — **lines 14–38**. Read-only here.
14. `README.md` — **lines 89–158** (local infrastructure; the configuration-contract table at **lines 133–139** whose third column says "ASP.NET Core configuration it will feed (CRM-106)"), **lines 183–199** (Common commands, incl. the `scripts/migrate` CRM-203 preview row), **lines 198–202** ("Migrations and tests … Both stories update this section when they land"). These are the places this story updates.
15. `src/backend/README.md` — **lines 25–34** (commands table), **lines 49–68** (layout tree), **lines 70–79** (allowed dependency directions), **lines 81–97** (the fixture's temporary status), **lines 124–142** (the "Non-goals" table whose row `| PostgreSQL / EF Core, schema-per-module persistence | CRM-106 |` must move out), **lines 144–155** (warnings-as-errors policy and the "Suppressions currently in place: none." claim that must stay true).
16. `src/backend/.editorconfig` — file-scoped namespaces (`error`), accessibility modifiers required, `readonly` fields. New code must satisfy these under `TreatWarningsAsErrors`.

Grep hints while implementing:

- `grep -rn "EntityFrameworkCore\|Npgsql\|ConnectionStrings" src/backend/src src/backend/tests --include=*.cs --include=*.csproj --include=*.json` — currently empty; use it afterwards to confirm nothing leaked into `*.Contracts` or `BuildingBlocks`.
- `grep -rn "CRM-106" README.md src/backend/README.md src/backend/tests` — every placeholder this story is expected to resolve.
- `grep -n "POSTGRES" env/backend.env.example docker-compose.yml` — the names to reuse verbatim.

---

## Decisions this plan makes (record these; do not re-litigate during implementation)

| Decision | Choice | Rationale |
|---|---|---|
| Context granularity | **One `DbContext` per module.** No `SquadCrmDbContext`. | ADR-002 + intake. Enforced by a static architecture rule. |
| Where module persistence lives | A `Persistence/` folder **inside the existing module implementation project**. No new `*.Persistence` project per module. | CRM-105 keeps a module to two projects; an extra project per module adds no enforcement the assembly-level rules do not already give. |
| **BuildingBlocks** | **Provider-neutral. No EF Core. No Npgsql. No PostgreSQL-specific detail.** Unchanged by this story. | CLAUDE.md: providers stay behind provider-neutral ports. A dedicated architecture rule now forbids **both** prefixes there. |
| **Where PostgreSQL configuration lives** | A new, small, provider-specific project **`src/Infrastructure/SquadCrm.Infrastructure.Postgres`** at the composition/infrastructure boundary. It references the **`Npgsql` ADO package only — never EF Core**. | The one implementation must be reachable by both the API composition root and each module's design-time factory; a module may not reference `SquadCrm.Api`, so the code cannot live in the host project itself. See the note below. |
| **EF Core** | **Only inside module implementation projects.** Not in the API host, not in BuildingBlocks, not in `Infrastructure.Postgres`, not in `*.Contracts`, not in ArchitectureTests. | Keeps the EF model a module concern. |
| How modules get the connection string | The API composition root derives it and publishes it **internally** as `ConnectionStrings:SquadCrmPostgres`; modules read it from the `IConfiguration` handed to `IModule.RegisterServices`. | No module needs a provider reference to be *composed*, and there is still exactly one operator contract: `POSTGRES_*`. |
| Fixture schema | `architecture_fixture` | Unmistakably non-business. Never `public`. |
| Fixture table | `persistence_probe` | Not a CRM concept; deletable with the fixture. |
| Identifier naming | lowercase `snake_case`, via **explicit** `ToTable`/`HasColumnName` in one `IEntityTypeConfiguration`. | Intake forbids adding a naming-conventions package for a three-column fixture. |
| Migration history | `architecture_fixture.__ef_migrations_history` — inside the owning module's schema. | Multiple contexts share one physical database; a shared `public.__EFMigrationsHistory` would collide across modules. |
| Applying migrations | Explicit `dotnet ef database update` via a **local tool manifest**. **No `Database.Migrate()` in `Program.cs`.** | Intake: never silently mutate a database at startup. |
| ArchitectureTests scope | **Purely static.** No `DbContext` instantiation, no design-time factory invocation, **no `Microsoft.EntityFrameworkCore.Relational` reference.** | Model metadata is runtime behaviour; proving it belongs to the integration suite. |
| IntegrationTests scope | All real database/schema behaviour, incl. default-schema and `public`-is-empty proof. Requires a running CRM-197 PostgreSQL and **fails loudly**, never skips. | AC 1/2/4 are unprovable without a real server. |
| CI orchestration / test filtering | **Deferred to CRM-202.** This story documents how to run each suite; it designs no CI pipeline. | Intake keeps the test-infrastructure strategy with CRM-202. |

**Note on the new `SquadCrm.Infrastructure.Postgres` project.** It exists to satisfy three constraints at once: (a) BuildingBlocks must stay provider-neutral; (b) the API runtime path and the design-time `IDesignTimeDbContextFactory` must share **one** implementation of reading, validating and assembling the connection string; (c) a module implementation may never reference `SquadCrm.Api`. It is a **provider-specific adapter at the infrastructure boundary**, not a second building-blocks layer and not a module: it contains configuration reading, validation, `NpgsqlConnectionStringBuilder` usage and redacted diagnostics, and nothing else. It is the one structural addition this story makes beyond CRM-105's layout — if it is rejected, the fallback is duplicating that logic in the host and in each module's factory, which contradicts correction 2, so raise it rather than silently forking the behaviour.

---

## Package versions — inspect, then pin

**Do not use floating versions, `*`, or "latest".** Observed on nuget.org at planning time (2026-08-26):

- `Npgsql` (ADO) and `Npgsql.EntityFrameworkCore.PostgreSQL` — highest stable **10.x** of the provider: **`10.0.3`** (11.x is preview only — **do not use a preview**). Pin the ADO `Npgsql` package to the version the provider resolves to, so the two never diverge.
- `Microsoft.EntityFrameworkCore.Design` / `dotnet-ef` — highest stable **10.x**: **`10.0.11`**, which matches the already-pinned `Microsoft.AspNetCore.OpenApi` `10.0.11` and `Microsoft.AspNetCore.Mvc.Testing` `10.0.11`.

Before writing the `csproj` entries, re-verify and pin the **highest stable `10.*`** of each:

```bash
for p in npgsql npgsql.entityframeworkcore.postgresql microsoft.entityframeworkcore.design dotnet-ef; do
  echo "== $p"; curl -s "https://api.nuget.org/v3-flatcontainer/$p/index.json" | tr ',' '\n' | grep -E '"10\.[0-9.]+"' | tail -3
done
```

Rules: every `Microsoft.EntityFrameworkCore.*` package on the **same** version; the provider is the official `Npgsql.EntityFrameworkCore.PostgreSQL`; **no** `EFCore.NamingConventions`, `EFCore.BulkExtensions`, `Testcontainers`, `Respawn`, and **no `Microsoft.EntityFrameworkCore.Relational` in ArchitectureTests**. Record the chosen versions and this rationale in `src/backend/README.md` (Task 10).

---

## Backend Tasks

### 1 — Add the EF tool manifest

**Create file: `src/backend/.config/dotnet-tools.json`** (via the CLI, from `src/backend/`, so the schema is correct):

```bash
cd src/backend
dotnet new tool-manifest
dotnet tool install dotnet-ef --version <pinned 10.x from above>
dotnet tool restore
dotnet ef --version
```

Commit `.config/dotnet-tools.json`. Every `dotnet ef` command in this plan and in the READMEs runs as `dotnet ef …` from `src/backend/` with the manifest restored — **never** as a globally installed tool, so two developers get the same tool version.

### 2 — One PostgreSQL configuration implementation, at the infrastructure boundary

**`SquadCrm.BuildingBlocks` is not touched by this task.** Do not add `Npgsql`, `Microsoft.EntityFrameworkCore*`, or any PostgreSQL-specific type to it. A new architecture rule (Task 7) makes that permanent.

**Create project: `src/backend/src/Infrastructure/SquadCrm.Infrastructure.Postgres/SquadCrm.Infrastructure.Postgres.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>SquadCrm.Infrastructure.Postgres</RootNamespace>
  </PropertyGroup>

  <!-- Provider-specific adapter at the composition/infrastructure boundary.
       ADO-level Npgsql ONLY: this project assembles and validates a connection
       string. EF Core belongs to module implementation projects, and
       SquadCrm.BuildingBlocks stays provider-neutral. Both facts are enforced
       by SquadCrm.ArchitectureTests. -->
  <ItemGroup>
    <PackageReference Include="Npgsql" Version="<pinned>" />
  </ItemGroup>

  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>

</Project>
```

Register it in the solution with `dotnet sln add` — **do not hand-edit the `.sln` GUID blocks.**

**Create file: `.../SquadCrm.Infrastructure.Postgres/PostgresOptions.cs`**

```csharp
namespace SquadCrm.Infrastructure.Postgres;

/// <summary>
/// The PostgreSQL coordinates, read from the single operator-facing contract
/// owned by CRM-197 (<c>POSTGRES_*</c> in <c>env/backend.env</c>).
/// <para>
/// The keys are deliberately flat (<c>POSTGRES_HOST</c>, not
/// <c>Postgres__Host</c>) because that is the contract Docker Compose and the
/// developer environment file already use. No second externally configured set
/// of database values is introduced.
/// </para>
/// </summary>
public sealed record PostgresOptions(
    string Host,
    int Port,
    string Database,
    string Username,
    string Password)
{
    public const string HostKey = "POSTGRES_HOST";
    public const string PortKey = "POSTGRES_PORT";
    public const string DatabaseKey = "POSTGRES_DB";
    public const string UsernameKey = "POSTGRES_USER";
    public const string PasswordKey = "POSTGRES_PASSWORD";

    /// <summary>
    /// Internal, conventional connection-string name derived from the keys above.
    /// It is an application-internal name, never an operator-facing setting: it is
    /// produced at composition time and is not read from any file or environment.
    /// </summary>
    public const string ConnectionStringName = "SquadCrmPostgres";
}
```

**Create file: `.../SquadCrm.Infrastructure.Postgres/PostgresConfiguration.cs`** — **the single implementation** used by *both* the API runtime path and every design-time factory. Nothing else in the repository may read `POSTGRES_*`, parse the port, or build a connection string:

- `ReadPostgresOptions(this IConfiguration configuration)` — reads the five keys; **fails fast** with one `InvalidOperationException` listing **every** missing/invalid key **by name only**. `POSTGRES_PORT` must parse as an integer in `1..65535`; an unparsable value is reported as `"POSTGRES_PORT must be an integer between 1 and 65535."` — **never echo the offending value**, and **never** include the password or the assembled connection string in any message. Mirror the wording style of `Program.cs` lines 31–33.
- `BuildConnectionString(this PostgresOptions options)` — uses `NpgsqlConnectionStringBuilder` (`Host`, `Port`, `Database`, `Username`, `Password`) so every part is escaped correctly. No pooling/timeout tuning — defaults are correct for this story.
- `Describe(this PostgresOptions options)` — the **only** permitted rendering for logs, exceptions and test output: `Host=…;Port=…;Database=…;Username=…`, **password omitted**. There must be no code path that logs, throws or serialises the password or the full connection string.
- `AddSquadCrmPostgres(this WebApplicationBuilder builder)` (or an `IHostApplicationBuilder` overload) — calls `ReadPostgresOptions()` + `BuildConnectionString()` **once**, then publishes the result into configuration as `ConnectionStrings:SquadCrmPostgres` via an in-memory configuration source added **last**, so it is available to every module through the `IConfiguration` that `RegisterModules` already passes. Also register `PostgresOptions` as a singleton for consumers that need the redacted description.

**Create file: `.../SquadCrm.Infrastructure.Postgres/PostgresConnectionStringAccessor.cs`** (or fold into the above) — `GetSquadCrmPostgresConnectionString(this IConfiguration configuration)`, which returns the derived value or throws a message telling the developer that `AddSquadCrmPostgres` must run in the composition root first. Modules call this instead of hand-writing the key name.

**File: `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj`** — add a `ProjectReference` to `SquadCrm.Infrastructure.Postgres`. **No EF Core package is added to the API host.**

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`** — add exactly one call, after the CORS block and before the module list (i.e. before line 57), in the file's existing voice:

```csharp
// PostgreSQL coordinates, read once from the POSTGRES_* operator contract
// (CRM-197), validated fail-fast, and published internally as
// ConnectionStrings:SquadCrmPostgres for each module's own DbContext.
// No migration runs here: schema changes are applied by an explicit
// `dotnet ef database update`.
builder.AddSquadCrmPostgres();
```

**Do not** add `Database.Migrate()`, a database health check, or a `ConnectionStrings` section to `appsettings.json`. `appsettings.json` and `appsettings.Development.json` stay unchanged — the values come from the environment.

**Regression this task creates, and its fix.** API startup now validates database configuration, so `SquadCrmApiFactory` (which boots the real host) fails when `POSTGRES_*` is absent — which is exactly the situation in a DB-less test run. Fix it in `SquadCrmApiFactory.ConfigureWebHost` (**lines 313–324**) by adding an in-memory configuration source supplying the five keys with **obviously fake, non-secret placeholder values** (e.g. host `localhost`, database/user `squadcrm-tests`, password a clearly-fake constant). No connection is opened by these tests, so the values are never used to reach a server. Document this in the factory's class comment in its existing voice, and keep `HealthEndpointTests` passing **with no database running** — that is the proof `/health` stayed a liveness probe.

### 3 — The module-owned persistence fixture

**File: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.csproj`**

Add the EF Core dependencies **here — this is the only project layer allowed to hold them** — plus a project reference to the infrastructure adapter (needed by the design-time factory in Task 4):

```xml
  <ItemGroup>
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="<pinned>" />
    <!-- Design-time only: enables `dotnet ef` against this module. Not shipped
         behaviour; PrivateAssets keeps it out of consumers. -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="<pinned>">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../../Infrastructure/SquadCrm.Infrastructure.Postgres/SquadCrm.Infrastructure.Postgres.csproj" />
  </ItemGroup>
```

**Create file: `.../SquadCrm.Modules.ArchitectureFixture/Persistence/ArchitectureFixtureSchema.cs`** — one static class holding `public const string Name = "architecture_fixture";`, `MigrationsHistoryTable = "__ef_migrations_history"`, and `ProbeTable = "persistence_probe"`, with a doc comment repeating that this is architecture scaffolding.

**Create file: `.../Persistence/PersistenceProbe.cs`**

```csharp
namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// <b>Architecture scaffolding — not a CRM concept.</b> The smallest possible
/// persisted row, existing only to prove that this module owns a schema, a
/// table and its own migrations. Delete it with the fixture once a real module
/// provides equivalent coverage.
/// </summary>
public sealed class PersistenceProbe
{
    public Guid Id { get; init; }

    /// <summary>Free-text marker written by the persistence verification test.</summary>
    public required string Label { get; init; }

    public DateTimeOffset RecordedAtUtc { get; init; }
}
```

**Create file: `.../Persistence/PersistenceProbeConfiguration.cs`** — an `IEntityTypeConfiguration<PersistenceProbe>` that sets `ToTable(ArchitectureFixtureSchema.ProbeTable, ArchitectureFixtureSchema.Name)`, `HasKey(p => p.Id)`, and explicit `HasColumnName("id" | "label" | "recorded_at_utc")`; `Label` `HasMaxLength(100).IsRequired()`; `RecordedAtUtc` `HasColumnType("timestamptz")`. **No foreign key of any kind** — cross-schema FKs are forbidden by the intake and there is nothing else to reference.

**Create file: `.../Persistence/ArchitectureFixtureDbContext.cs`**

```csharp
public sealed class ArchitectureFixtureDbContext(DbContextOptions<ArchitectureFixtureDbContext> options)
    : DbContext(options)
{
    public DbSet<PersistenceProbe> PersistenceProbes => Set<PersistenceProbe>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        // Every table this module owns lives in its own schema — never `public`,
        // and never another module's schema.
        modelBuilder.HasDefaultSchema(ArchitectureFixtureSchema.Name);
        modelBuilder.ApplyConfiguration(new PersistenceProbeConfiguration());
    }
}
```

The class doc must state: this context maps **only** this module's model; it must never expose another module's `DbSet` or entity, and must never query another module's schema — by EF mapping, by raw SQL, or through a view.

### 4 — Shared runtime/design-time configuration, and the design-time factory

**Create file: `.../Persistence/ArchitectureFixtureDbContextOptions.cs`** — one **internal** helper that applies `UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(ArchitectureFixtureSchema.MigrationsHistoryTable, ArchitectureFixtureSchema.Name))` to a `DbContextOptionsBuilder`. Both the runtime registration (Task 5) and the design-time factory call it, so **the provider options and the migrations-history placement can never diverge between the two paths.**

**Create file: `.../Persistence/ArchitectureFixtureDbContextFactory.cs`** — an `IDesignTimeDbContextFactory<ArchitectureFixtureDbContext>` so `dotnet ef` can target this class library with no runtime host and no startup hacks. It must:

- build an `IConfiguration` from **the process environment only** — `new ConfigurationBuilder().AddEnvironmentVariables().Build()`;
- call the **same** `ReadPostgresOptions()` / `BuildConnectionString()` from `SquadCrm.Infrastructure.Postgres` that the API composition root uses — **no second parser, no second validator, no duplicated port check, no duplicated redaction**;
- apply the shared `ArchitectureFixtureDbContextOptions` helper above.

**It must not read, locate or parse `env/backend.env` itself.** No `DotNetEnv`-style package, no file probing, no walking up the directory tree. Loading the developer env file into the process environment is a **developer workflow step**, documented in Task 10 and repeated wherever a `dotnet ef` command appears:

```bash
# From the repository root, load the CRM-197 developer values into this shell:
set -a && . ./env/backend.env && set +a
cd src/backend
# PowerShell equivalent: Get-Content env/backend.env | ForEach-Object { ... $env:... }
```

If a required key is absent the factory fails with the same fail-fast message the API produces — one message, one implementation, naming keys only.

### 5 — Register the context from the module (not from the host)

**File: `.../SquadCrm.Modules.ArchitectureFixture/ArchitectureFixtureModule.cs`**

Inside `RegisterServices` (after line 44), add:

```csharp
// The module registers its OWN DbContext, using the connection string the
// composition root derived from the POSTGRES_* contract. The host never sees
// this type, and no other module may reference it (SquadCrm.ArchitectureTests
// enforces this).
services.AddDbContext<ArchitectureFixtureDbContext>(options =>
    ArchitectureFixtureDbContextOptions.Apply(
        options, configuration.GetSquadCrmPostgresConnectionString()));
```

Extend the class doc comment (lines 11–26) with one sentence: the fixture now also proves **module-owned persistence** (schema, table, migrations, migration history), removable on the same terms as the rest of the fixture.

**Do not** add an endpoint that reads or writes the probe table. `/internal/architecture-fixture/module-info` stays exactly as it is: **database details must not be exposed through any API surface.**

### 6 — Create the module's initial migration

Run from `src/backend/` after loading the env values into the shell (Task 4). PostgreSQL does **not** need to be running — scaffolding is offline, but the five keys must be present:

```bash
dotnet ef migrations add InitialArchitectureFixturePersistence \
  --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture \
  --context ArchitectureFixtureDbContext \
  --output-dir Persistence/Migrations
```

The generated files land under `.../SquadCrm.Modules.ArchitectureFixture/Persistence/Migrations/` — **with the owning module, never in a central migrations folder.** Inspect the generated `Up` before committing and confirm it contains `migrationBuilder.EnsureSchema(name: "architecture_fixture")` and `schema: "architecture_fixture"` on the `CreateTable` call. If either is missing, the `HasDefaultSchema`/`ToTable` configuration is wrong — fix the model, delete the migration, regenerate. **Never hand-edit generated migration SQL to patch a modelling mistake.**

Commit the migration `.cs` files **and** the `*ModelSnapshot.cs`.

### 7 — Architecture rules (static only)

**File: `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs`** — add `public static Assembly InfrastructurePostgres { get; } = typeof(PostgresOptions).Assembly;` and include it in `All` (lines 30–38), keeping the "reference one type per assembly" pattern from lines 8–11.

**File: `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`** — update `ForbiddenAssemblyPrefixes` (lines 79–91): **remove** `"Microsoft.EntityFrameworkCore"` and `"Npgsql"` from the *solution-wide* list — CRM-106 legitimately introduces both, in specific projects — and update the doc comment (lines 74–78) so it no longer names CRM-106. `Hangfire`, `MediatR`, `FluentValidation`, the authentication/authorization prefixes and the OpenAPI-UI prefixes **stay forbidden**. The narrower per-project rules below replace the blanket ban.

**Create file: `src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs`** — same style as the existing rules (assert over IL with NetArchTest **and** over `ReferencedAssemblyNames`), reusing `SquadCrmAssemblies`. **No new package reference. No `DbContext` instantiation. No design-time factory invocation. No EF model metadata inspection.** Types are examined by name and by base type via reflection only:

1. `ModuleContracts_MustNotDependOnEfCoreOrNpgsql` — for every `*.Contracts` assembly in `SquadCrmAssemblies.All`, no referenced assembly starts with `Microsoft.EntityFrameworkCore` or `Npgsql`.
2. `BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` — **`SquadCrm.BuildingBlocks` references neither prefix.** This is the rule that keeps BuildingBlocks provider-neutral; state that intent in the test's doc comment so a future "just one little Npgsql helper" change fails loudly.
3. `BuildingBlocks_MustNotDependOnModulePersistenceOrInfrastructure` — extends the existing lines 94–109 rule: no type in BuildingBlocks depends on any `SquadCrm.Modules.*` or `SquadCrm.Infrastructure.*` namespace.
4. `InfrastructurePostgres_MustNotDependOnEfCoreModulesOrApi` — the adapter is a leaf: it references no `Microsoft.EntityFrameworkCore*` assembly, no `SquadCrm.Modules.*`, and not `SquadCrm.Api`.
5. `Api_MustNotDependOnModulePersistenceInternals` — no type in `SquadCrm.Api` depends on a `SquadCrm.Modules.*.Persistence` namespace, and `SquadCrm.Api` references no `Microsoft.EntityFrameworkCore*` assembly. The host composes modules; it never touches their EF internals. (The host still *references* the module assembly — that is the CRM-105 composition seam — so this rule is expressed at namespace level, not assembly level.)
6. `Modules_MustNotDependOnAnotherModulesPersistenceNamespace` — generic over every module implementation assembly: no dependency on a `SquadCrm.Modules.*.Persistence` namespace other than its own.
7. `EveryDbContext_MustLiveInItsOwningModulePersistenceNamespace` — over `SquadCrmAssemblies.All`, every concrete type whose base-type chain reaches `Microsoft.EntityFrameworkCore.DbContext` (matched **by full type name**, so ArchitectureTests needs no EF reference) must have a namespace matching `SquadCrm.Modules.<Module>.Persistence`, and `<Module>` must match its own assembly. This is the rule that fails if anyone reintroduces a central `SquadCrmDbContext` or parks a context in the host, BuildingBlocks or the infrastructure adapter. Fail messages name the offending type.

**Document honestly** (in a class-level comment mirroring lines 56–71, and in `src/backend/README.md`): these rules prove **structural** boundaries — assembly and namespace dependency direction, and context placement. They **cannot** prove which SQL runs at runtime, and they deliberately do not inspect the EF model. Default schema, table placement and `public` emptiness are proven by `SquadCrm.Persistence.IntegrationTests` against a real server. Cross-module SQL remains a coding convention until DB-level roles are deliberately introduced (explicitly not CRM-106).

### 8 — Persistence integration tests

**Create project: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/SquadCrm.Persistence.IntegrationTests.csproj`** — mirror `SquadCrm.Api.Tests.csproj` (`net10.0`, `IsPackable=false`, `coverlet.collector` 6.0.4, `Microsoft.NET.Test.Sdk` 17.14.1, `xunit` 2.9.3, `xunit.runner.visualstudio` 3.1.4, `<Using Include="Xunit" />`), plus project references to `SquadCrm.Infrastructure.Postgres` and `SquadCrm.Modules.ArchitectureFixture`. Add it with `dotnet sln add`.

**Create file: `.../PostgresTestDatabase.cs`** — an xUnit fixture that:

- reads configuration through the **same** `ReadPostgresOptions()` implementation (no test-local parsing);
- builds the context through the module's design-time factory;
- calls `Database.MigrateAsync()` once. Test-only migration application is fine and is **not** startup migration;
- when the server is unreachable, **throws** with an actionable message — `"PostgreSQL is not reachable at <host>:<port>. Run `docker compose up -d` from the repository root."` — built from `Describe()`, so **no password and no full connection string** appear. These tests **must fail, never silently skip**: a green run must mean the database was really exercised.

This suite owns every claim about real database state (schema, table, columns, history table, `public` emptiness, pending migrations). See the Test Plan.

### 9 — Frontend

**No frontend changes required.** Nothing under `src/frontend/` is touched by this story.

### 10 — Documentation

**File: `src/backend/README.md`**

- Commands table (lines 29–34): add `dotnet tool restore`; the env-loading step (`set -a && . ../../env/backend.env && set +a` from `src/backend/`, with the PowerShell equivalent) as a **prerequisite of every `dotnet ef` command**; `dotnet ef migrations add …`; `dotnet ef database update …`; and a note that `dotnet test` needs `docker compose up -d`.
- Layout tree (lines 51–68): add `src/Infrastructure/SquadCrm.Infrastructure.Postgres/`, the module `Persistence/` folder (with `Migrations/`) and the new test project.
- Allowed dependency directions (lines 70–79): add — `BuildingBlocks` → **never** EF Core or Npgsql (provider-neutral); `*.Contracts` → **never** EF Core or Npgsql; `SquadCrm.Infrastructure.Postgres` → Npgsql (ADO) only, never EF Core, never a module, never the host; EF Core → **module implementation projects only**; `SquadCrm.Api` → the infrastructure adapter and modules, never a module's persistence internals.
- New section **"Persistence"** documenting, in this order: `DbContext` per module (and why there is no shared context); schema ownership and the `architecture_fixture` schema; **where PostgreSQL configuration lives and why it is not in BuildingBlocks**; the `POSTGRES_*` → `ConnectionStrings:SquadCrmPostgres` derivation, stated as an **internal** name over a **single** operator contract; that runtime and design-time share one implementation and the factory reads **only** the process environment; the env-loading workflow before `dotnet ef`; the migration-history strategy (`architecture_fixture.__ef_migrations_history`) and why a shared history table is wrong here; the exact migration commands; **how to add persistence to a future module** (schema constant, entity + configuration, context, options helper, design-time factory, register in that module's `RegisterServices`, generate migrations into that module — and *do not* touch BuildingBlocks); how to reset and recreate the local database; the chosen EF Core/Npgsql versions and rationale.
- New subsection **"What the tests prove"**: ArchitectureTests = static structure only; IntegrationTests = real schema/table/history/`public`/pending-migration behaviour; the full `dotnet test` therefore **requires** the CRM-197 PostgreSQL service, while `dotnet test tests/SquadCrm.Api.Tests` and `dotnet test tests/SquadCrm.ArchitectureTests` run without a database. **CI orchestration and test filtering are CRM-202's, not designed here.**
- The fixture section (lines 81–97): the persistence fixture is temporary and removable on the same terms — with its schema and migrations — once a real module provides equivalent coverage.
- Non-goals table (lines 130–139): **remove** the `| PostgreSQL / EF Core, schema-per-module persistence | CRM-106 |` row; add explicit "CRM-106 deliberately does not solve" bullets: cross-module distributed transactions, the transactional outbox (CRM-198), per-module PostgreSQL roles/permissions, production migration automation, startup migration, database readiness in `/health` (CRM-201), CI test orchestration (CRM-202).
- Keep the liveness-only `/health` statement (lines 141–142) and the "Suppressions currently in place: none." claim (line 154) **true**.

**File: `README.md`**

- Configuration-contract table (lines 133–139): put the third column in the present tense and note the values are derived **once at composition time** into the internal `ConnectionStrings:SquadCrmPostgres`; state plainly that `POSTGRES_*` remains the **only** database configuration an operator sets.
- "Migrations and tests" (lines 198–202): replace the CRM-106 placeholder with the real flow — `docker compose up -d` → load `env/backend.env` into the shell → `cd src/backend && dotnet tool restore && dotnet ef database update …` → verify → `docker compose down -v` → repeat. Mark `docker compose down -v` **DESTRUCTIVE** in the voice of lines 126–128. Note that the full backend test run needs the database up.
- Common commands table (lines 183–197): add the `dotnet ef database update` row as "available today". **Leave the `scripts/*` CRM-203 preview row alone.**

Do not modify `docs/adr/*`, `env/backend.env.example`, `docker-compose.yml`, `.gitignore`, or any `.squad/` file other than this feature's overview and the plans index.

---

## Edge Cases & Failure Modes

- **A required `POSTGRES_*` key is missing.** The shared `ReadPostgresOptions` throws one `InvalidOperationException` naming **all** missing keys, before any connection is attempted. Identical behaviour at API startup, at design time and in tests, because there is one implementation (Task 2/4).
- **`POSTGRES_PORT` is non-numeric or out of range.** Same fail-fast path; the message names the key and the valid range and **never echoes the value** (it can carry pasted junk).
- **A password containing `;`, `'` or `=`.** Handled because the connection string is assembled by `NpgsqlConnectionStringBuilder`, never by string concatenation.
- **Secret leakage.** No exception message, log line, test output or API response may contain the password or the assembled connection string; only `Describe()` is used. Verified by a unit test and by the greps in Verification step 9.
- **API tests run with no database configuration.** `SquadCrmApiFactory` supplies placeholder `POSTGRES_*` values (Task 2) so host startup validation passes without a server; no connection is opened. If this is ever removed, `HealthEndpointTests` fails at startup — that failure is the intended signal, not a reason to weaken the fail-fast validation.
- **`dotnet ef` run without loading `env/backend.env`.** The factory reads the process environment only and fails fast naming the missing keys. The fix is the documented `set -a && . ./env/backend.env && set +a`, **never** teaching the factory to find the file.
- **PostgreSQL is not running.** The API host starts (the connection is lazy) and fails on first database use; the integration tests fail immediately with the actionable "run `docker compose up -d`" message. They must **fail**, never skip. A DB-less run is still possible per-project (`dotnet test tests/SquadCrm.Api.Tests`, `dotnet test tests/SquadCrm.ArchitectureTests`); wiring that into CI is CRM-202's.
- **Port 5432 already taken locally.** `POSTGRES_PORT` moves the published host port (README lines 148–152); because the same key feeds Compose, the backend and `dotnet ef`, no second value needs changing.
- **Credentials changed after first container start.** PostgreSQL only honours `POSTGRES_USER`/`PASSWORD`/`DB` on an empty data directory (README lines 165–168). Symptom is an authentication failure with correct-looking configuration; the fix is the destructive `docker compose down -v`. Document this next to the migration commands.
- **Half-applied migration state.** A migration that fails mid-way leaves `architecture_fixture.__ef_migrations_history` inconsistent with the schema. Local remedy: `docker compose down -v && docker compose up -d && dotnet ef database update …`. This story automates no production migration, so it defines no production recovery path.
- **Two module contexts, one database.** Because each context's history table lives in its own schema, `dotnet ef database update` for one module can never mark another module's migrations as applied. A future module that forgets `MigrationsHistoryTable(...)` writes to `public.__EFMigrationsHistory` — the integration suite's `public`-is-empty test is what catches that, since the architecture rules deliberately do not inspect EF metadata.
- **Concurrent `dotnet ef database update` runs.** EF takes a lock; the second run waits or fails. A developer-workflow hazard, not something this story automates.
- **`dotnet ef` cannot find a context.** The `IDesignTimeDbContextFactory` is the deterministic answer; if a command still fails, pass `--context ArchitectureFixtureDbContext` explicitly — **do not** add a startup-project hack or change `Program.cs`.
- **`TreatWarningsAsErrors` + generated migrations.** EF-generated files can trip style analysers. Apply the **narrowest** suppression allowed by `src/backend/README.md` lines 144–152 — a scoped `#pragma` or a `NoWarn` on the single project — comment it inline, and record it in the README's suppressions note, which must then stop saying "none".
- **Unicode/long `Label` values.** `Label` is `HasMaxLength(100)`; a longer value throws on save. The probe table is scaffolding, so no truncation behaviour is designed.

---

## Test Plan

Static / unit (no database required):

1. **Add** `src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs` with the seven rules in Task 7. Match the naming and dual-assertion style of `ArchitectureRulesTests.cs` lines 93–145. **No EF Core package reference, no `DbContext` construction, no design-time factory call** — `DbContext` is matched by full base-type name via reflection.
2. **Modify** `ArchitectureRulesTests.ForbiddenAssemblyPrefixes` (lines 79–91) to drop the two now-legitimate prefixes, and confirm `Foundation_MustNotIntroduceForbiddenDependencies` still passes for `Hangfire`, `MediatR`, `FluentValidation`, auth and OpenAPI-UI prefixes.
3. **Modify** `SquadCrmAssemblies.cs` to include `SquadCrm.Infrastructure.Postgres` in `All`, so every solution-wide rule covers it.
4. **Add** `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresConfigurationTests.cs` (unit, **no server**): missing keys produce one exception naming every missing key; a non-numeric and an out-of-range `POSTGRES_PORT` are both rejected naming the key only; a password containing `;` and `=` round-trips through `NpgsqlConnectionStringBuilder`; **`Describe()` never contains the password**, asserted against a sentinel password value in the style of `SquadCrmApiFactory.SentinelMessage`.
5. **Modify** `src/backend/tests/SquadCrm.Api.Tests/SquadCrmApiFactory.cs` per Task 2 (placeholder `POSTGRES_*` values). Leave every existing API test assertion unchanged; `HealthEndpointTests` must still pass **with no database running**.

Integration (requires `docker compose up -d`; these tests fail, never skip, when it is not):

6. **Add** `.../ConnectionTests.cs` — `Connection_OpensAgainstConfiguredPostgres`: opens an `NpgsqlConnection` built by the shared implementation and asserts `State == Open`.
7. **Add** `.../MigrationTests.cs` — `Migrations_ApplyToACleanDatabase` (`Database.MigrateAsync()` succeeds) and `NoMigrationsRemainPending` (`Database.GetPendingMigrationsAsync()` is empty).
8. **Add** `.../SchemaOwnershipTests.cs`:
   - `Schema_ExistsForTheOwningModule` — `information_schema.schemata` contains `architecture_fixture`.
   - `Table_ExistsInModuleSchema` — `information_schema.tables` contains `architecture_fixture.persistence_probe`.
   - `Columns_UseSnakeCaseNames` — `information_schema.columns` for that table returns exactly `id`, `label`, `recorded_at_utc`.
   - `MigrationHistory_LivesInModuleSchema` — `architecture_fixture.__ef_migrations_history` exists and holds at least one row.
   - `PublicSchema_HoldsNoSquadCrmTables` — `information_schema.tables` where `table_schema = 'public'` returns no rows. **This is the test that proves no module leaked into `public`**, replacing the EF-metadata rule that ArchitectureTests deliberately no longer performs.
9. **Add** `.../PersistenceRoundTripTests.cs` — `Probe_CanBeWrittenAndReadBack`: insert a `PersistenceProbe` inside an EF transaction on the module's own context, read it back, assert the `timestamptz` value round-trips. This is the "a module uses normal EF transactions within its own context" case; **no cross-module transaction is attempted.**
10. **No test may print a password or a full connection string.** Assert the redacted rendering explicitly (test 4) rather than trusting review.

---

## Migration / Rollback

- **Forward:** `docker compose up -d` → load the env values into the shell (`set -a && . ./env/backend.env && set +a`) → `cd src/backend && dotnet tool restore` → `dotnet ef database update --project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --startup-project src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture --context ArchitectureFixtureDbContext`.
- **Rollback of the schema:** `dotnet ef database update 0 --project … --context ArchitectureFixtureDbContext` drops the fixture objects. Locally, `docker compose down -v` (**destructive**) followed by `up -d` is equally valid and is the documented reset.
- **Rollback of the story:** deleting `src/Infrastructure/SquadCrm.Infrastructure.Postgres`, the module `Persistence/` folder, the one `Program.cs` call and its project reference, the `SquadCrmApiFactory` placeholder block, the new test project and the new architecture-rule file returns the repository to the CRM-105/CRM-197 state. `SquadCrm.BuildingBlocks` is untouched by this story, so nothing there needs reverting. There is no production database in play.
- **Half-applied state:** see Edge Cases. Local recovery is always volume reset + re-apply; this story defines no production recovery because it automates no production migration.

---

## Verification Steps

1. **Backend restores:** from `src/backend/` — `dotnet tool restore && dotnet restore`. Both succeed; `dotnet ef --version` prints the pinned version.
2. **Backend builds:** from `src/backend/` — `dotnet build -warnaserror`. **Zero errors and zero warnings** under the existing `TreatWarningsAsErrors` policy. Any suppression is narrow, inline-commented, and recorded in `src/backend/README.md`.
3. **BuildingBlocks stayed provider-neutral:**
   ```bash
   git diff --stat -- src/backend/src/BuildingBlocks   # expected: no changes at all
   grep -rn "Npgsql\|EntityFrameworkCore" src/backend/src/BuildingBlocks
   ```
   The grep must return **nothing**, and the architecture rule `BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` must pass.
4. **Infrastructure healthy:** from the repository root — `export COMPOSE_ENV_FILES=env/backend.env && docker compose up -d && docker compose ps`. The `postgres` service reports `healthy`.
5. **Migrations apply:** after loading the env values, from `src/backend/` — the `dotnet ef database update …` command exits 0, and `dotnet ef migrations list --project … --context ArchitectureFixtureDbContext` shows the initial migration as applied. Run the same command **without** loading the env file and confirm it fails fast naming the missing keys and **printing no value**.
6. **Expected artefacts exist:**
   ```bash
   docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c '\dt architecture_fixture.*'
   docker compose exec postgres psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" \
     -c "select table_schema, table_name from information_schema.tables where table_schema not in ('pg_catalog','information_schema');"
   ```
   Output shows `architecture_fixture.persistence_probe` and `architecture_fixture.__ef_migrations_history`, and **nothing in `public`**.
7. **Clean recreation:** `docker compose down -v && docker compose up -d`, then repeat steps 5–6 from an empty volume. Everything is recreated **from migrations alone** — no init SQL, no manual `CREATE SCHEMA`.
8. **Tests:**
   - With the database up, from `src/backend/` — `dotnet test`. Architecture, API and persistence suites all pass.
   - With the database **down** (`docker compose down`) — `dotnet test tests/SquadCrm.Api.Tests` and `dotnet test tests/SquadCrm.ArchitectureTests` both pass, proving neither has a database dependency; `dotnet test tests/SquadCrm.Persistence.IntegrationTests` **fails with the actionable message** — confirm it **fails** rather than reporting skipped/passed.
9. **Scope and secret checks:**
   ```bash
   grep -rn "EntityFrameworkCore\|Npgsql" src/backend/src/BuildingBlocks src/backend/src/Modules/*/*.Contracts src/backend/src/Api --include=*.csproj --include=*.cs
   grep -rn "EntityFrameworkCore" src/backend/src/Infrastructure src/backend/tests/SquadCrm.ArchitectureTests --include=*.csproj
   grep -rn "Database.Migrate\|MigrateAsync" src/backend/src
   grep -rn "SquadCrmDbContext\|ApplicationDbContext" src/backend
   grep -rn "POSTGRES_" src/backend --include=*.cs | grep -v Infrastructure.Postgres
   grep -rn "Hangfire\|Outbox\|Authentication" src/backend/src
   grep -rniE "password" src/backend/src --include=*.cs
   ```
   Expected: the first matches **only** the `SquadCrm.Api` → `Infrastructure.Postgres` project reference (never an EF Core or Npgsql package in BuildingBlocks, `*.Contracts` or the host); the second matches **nothing** (no EF Core in the adapter or in ArchitectureTests); the third matches **nothing under `src/`** (test-only migration lives in `tests/`); the fourth matches nothing; the fifth matches nothing outside the adapter (and the test placeholders in `tests/`), proving one configuration implementation; the sixth matches nothing; the seventh shows only configuration-key names and redaction code — never a logged or thrown value.
10. **Frontend regression:** `cd src/frontend && npm run lint && npm run build` — unchanged and green. No frontend file is in the diff.

---

## Done Criteria

- [ ] `SquadCrm.BuildingBlocks` is **unchanged** and depends on neither `Microsoft.EntityFrameworkCore*` nor `Npgsql*`, enforced by an architecture rule.
- [ ] PostgreSQL configuration reading, validation, connection-string construction and redacted diagnostics exist in **exactly one** implementation, at the composition/infrastructure boundary, used by both the API runtime path and the design-time factory.
- [ ] The design-time factory reads the **process environment only** — it never locates or parses `env/backend.env` — and the env-loading workflow is documented next to every `dotnet ef` command.
- [ ] `POSTGRES_*` remains the **only** operator-facing database configuration; `ConnectionStrings:SquadCrmPostgres` is derived internally at composition time and is never something an operator sets.
- [ ] EF Core packages appear **only** in module implementation projects; the API host, the infrastructure adapter, BuildingBlocks, `*.Contracts` and ArchitectureTests carry none.
- [ ] Missing or invalid database configuration fails fast with a message naming the keys and never containing a password or connection string, identically at runtime and design time.
- [ ] EF Core and the official Npgsql provider are pinned to explicit, aligned stable `10.*` versions, with versions and rationale in `src/backend/README.md`.
- [ ] The `ArchitectureFixture` module owns its `DbContext`, its `architecture_fixture` schema, its `persistence_probe` table and its migrations, all inside the module's own project.
- [ ] Migration history is `architecture_fixture.__ef_migrations_history`; no shared history table exists.
- [ ] Migrations are applied by an explicit `dotnet ef` command via the committed local tool manifest; `Program.cs` contains no `Database.Migrate()`.
- [ ] A destroyed local volume can be recreated from migrations alone, with no init SQL and no manual schema creation.
- [ ] ArchitectureTests are **purely static** — no `DbContext` or factory instantiation, no `Microsoft.EntityFrameworkCore.Relational` reference — and cover: Contracts free of EF Core/Npgsql; BuildingBlocks free of EF Core/Npgsql; the adapter free of EF Core/modules/host; no cross-module persistence dependency; the API free of module persistence internals; every `DbContext` inside its owning `SquadCrm.Modules.<Module>.Persistence` namespace; no shared/application-wide `DbContext`.
- [ ] IntegrationTests prove, against real PostgreSQL: schema exists, table exists in that schema, module-specific history table exists, **no Squad CRM table in `public`**, migrations apply, no migrations pending, clean-volume recreation works.
- [ ] Persistence integration tests **fail loudly** when PostgreSQL is unavailable and are never silently skipped; the README states that the full `dotnet test` needs the CRM-197 service, that the DB-independent suites can be run on their own, and that CI orchestration/filtering is CRM-202's.
- [ ] `dotnet build` produces zero warnings; any suppression is narrow, commented and recorded.
- [ ] `src/backend/README.md` documents what is structurally enforced **and** what remains a coding convention, plus the explicit CRM-106 non-goals.
- [ ] Root `README.md` and `src/backend/README.md` resolve their CRM-106 placeholders; no ADR, Compose file, env example key, frontend file or other unrelated file is modified.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 06.**
