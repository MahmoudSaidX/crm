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
| `dotnet restore` | Restore NuGet packages |
| `dotnet build` | Build the solution |
| `dotnet run --project src/Api/SquadCrm.Api` | Run the API host |
| `dotnet test` | Run architecture and API integration tests |

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

## Layout

```
src/backend/
├── SquadCrm.sln
├── global.json
├── Directory.Build.props
├── src/
│   ├── Api/
│   │   └── SquadCrm.Api/                                (ASP.NET Core Web API host / composition root)
│   ├── BuildingBlocks/
│   │   └── SquadCrm.BuildingBlocks/                     (technical cross-cutting only)
│   └── Modules/
│       └── ArchitectureFixture/
│           ├── SquadCrm.Modules.ArchitectureFixture.Contracts/     (public contract surface)
│           └── SquadCrm.Modules.ArchitectureFixture/               (implementation)
└── tests/
    ├── SquadCrm.ArchitectureTests/                      (xUnit + NetArchTest.Rules)
    └── SquadCrm.Api.Tests/                              (xUnit + WebApplicationFactory)
```

### Allowed dependency directions

Enforced by `SquadCrm.ArchitectureTests`; a violation fails `dotnet test`.

- `SquadCrm.Api` → `BuildingBlocks`, the module implementation and its contracts
  (composition root only).
- A module implementation → `BuildingBlocks` and its own contracts.
- `*.Contracts` → no project references at all.
- `BuildingBlocks` → never a module or the API host.
- A module → never another module's implementation assembly; only its `*.Contracts`.

## The `ArchitectureFixture` module

`SquadCrm.Modules.ArchitectureFixture` and its endpoint
`GET /internal/architecture-fixture/module-info` are **infrastructure-only
architecture scaffolding**, not a CRM capability. The name is deliberately
non-business. Their only purpose is to prove:

- explicit `IModule` registration by the host (no runtime assembly scanning);
- the contracts-vs-implementation split;
- module endpoint composition;
- the dependency rules above.

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

## Non-goals in this foundation

Owned by later stories and intentionally absent here — the absence is enforced by
architecture rule `Foundation_MustNotIntroduceForbiddenDependencies`, which those
stories must update when they legitimately introduce a dependency:

| Not here | Owning story |
|---|---|
| PostgreSQL / EF Core, schema-per-module persistence | CRM-106 |
| Integration events / transactional outbox | CRM-198 |
| File storage adapters | CRM-200 |
| OpenTelemetry observability pipeline | CRM-201 |
| Broader testing infrastructure | CRM-202 |
| Authentication / authorization / sessions | CRM-204, CRM-110 |
| External integrations | CRM-192 |
| Hangfire, HTTPS/deployment, API versioning | later stories |

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

**Suppressions currently in place: none.** The solution builds clean with zero
warnings.
