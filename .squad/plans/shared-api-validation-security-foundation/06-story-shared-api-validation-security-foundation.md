# Story 06 — Shared API, Validation & Security Foundation (Story: CRM-204)

## Amendment (approved, in force — supersedes any conflicting text below)

The user approved five decisions after initial plan review. Where any text
below conflicts with these, this section governs:

- **D1 — Error `code`.** RFC 9457 body gains a stable machine-readable `code`
  extension member plus a localization key derived from it. The shared
  building block owns only the wire convention and plumbing (format, how a
  code is attached) — **no centralized mega-enum** of every module's error
  codes. Each capability/module declares and owns its own codes under the
  shared convention. Resolves the hardcoded-English-message finding: messages
  become localizable against the code, or are recorded as developer-facing
  text explicitly keyed by code.
- **D2 — Correlation.** `correlationId` and `traceId` are DISTINCT and must
  never be documented/tested/relied upon as equal. `correlationId` is the
  stable client/support handle (`CorrelationIdMiddleware`'s value, echoed via
  `X-Correlation-Id`). `traceId` is the observability/tracing identifier. The
  error body gains an explicit `correlationId` extension sourced from the
  middleware. The "always matches traceId" convention text and its equality
  test are removed; replaced by a test asserting they DIVERGE when an
  `Activity` is active.
- **D3 — Current user port.** `ICurrentUserAccessor` is narrowed to
  `IsAuthenticated` + an opaque handle only — no `UserId`/`OrganizationScope`,
  no subject-kind discriminator, no identity model (CRM-110 owns that).
  `AnonymousCurrentUserAccessor` is DELETED; no default implementation is
  registered, so a missing registration fails DI resolution (fail-closed)
  instead of returning a plausible anonymous answer.
- **D4 — Success responses.** Bare DTOs for single-resource/normal success.
  Pagination uses `PagedResult<T>`. Errors use RFC 9457. Cross-cutting
  response metadata travels in headers unless explicitly designed otherwise.
  Recorded as an explicit refinement of `docs/adr/ADR-003-api-errors.md`
  (the one ADR edit this plan authorizes) and in `docs/api-conventions.md`.
- **D5 — API versioning.** `/api/v1` route prefix convention adopted now —
  route-prefix + documentation only, no version-negotiation framework, no
  broader versioning machinery. The fixture demo endpoints adopt the prefix.

Also folded in as implementation-level corrections: `ValidationEndpointFilter<T>`
must be attached to a real endpoint boundary (it currently is attached to
none); `SecurityHeadersMiddleware` registers right after
`app.UseExceptionHandler()` so 500 responses carry the headers too; HSTS
gates on `IHostEnvironment`, not `Request.IsHttps`; `MaxPageSize` is
`static readonly`, not `const`; the new namespace is
`SquadCrm.BuildingBlocks.Http` (not `.Api`); `CorrelationIdTests.cs`,
`CorsTests.cs`, `HealthEndpointTests.cs`, `ValidationFoundationTests.cs`
already exist in `tests/SquadCrm.Api.Tests/` and are extended, not created.

---

## Prerequisites

- **Story 03 completed (CRM-105 — ASP.NET Core Modular Monolith Foundation).** See [`../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md`](../crm-105-aspnet-core-modular-monolith/02-story-crm-105-aspnet-core-modular-monolith.md). CRM-105 already shipped a working slice of this story's scope: RFC 9457 Problem Details (`src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Errors/`), `CorrelationIdMiddleware`, a DataAnnotations `ValidationEndpointFilter<T>`, and environment-aware CORS with fail-fast wildcard rejection in `Program.cs`. This story **generalizes and extends** that work; it does not replace it.
- **Story 05 completed (CRM-106 — PostgreSQL + EF Core + Schema-per-Module).** See [`../crm-106-postgresql-ef-core-schema-per-module/05-story-crm-106-postgresql-ef-core-schema-per-module.md`](../crm-106-postgresql-ef-core-schema-per-module/05-story-crm-106-postgresql-ef-core-schema-per-module.md). Not touched by this story — persistence is out of scope here.
- **`docs/adr/ADR-003-api-errors.md` and `docs/adr/ADR-004-auth-authorization.md` are binding read-only inputs.** ADR-003: "explicit DTOs, consistent validation/errors, bounded pagination, correlation IDs and safe exception mapping." ADR-004: "keep staff/customer identity semantics distinct; support revocable sessions; enforce Permission + Organizational Scope + Resource Ownership on backend." This story prepares the **extension point** for ADR-004, it does not implement a scheme. **Do not amend either ADR.**
- `src/backend/README.md` line 380 already lists this story: `| Authentication / authorization / sessions | CRM-204, CRM-110 |` under "Non-goals in this foundation" — CRM-204 prepares the extension point, CRM-110 implements the real thing.
- `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` lines 24–27 already say: *"CRM-198 (events/outbox) and CRM-204/CRM-110 (auth) must update this list when they legitimately introduce one of these."* — this story is that update, for the `Microsoft.AspNetCore.Authorization.` prefix only (see Task 5).
- Coordinate with the owners of the stories this one unblocks — CRM-110 (auth), CRM-114 (audit), CRM-198 (events/outbox), CRM-200 (file storage), CRM-201 (OpenTelemetry), CRM-202 (test infrastructure). **Implement none of them here.**

---

## Story Goal

Give every future module a predictable, secure, already-proven application-boundary contract so it never has to invent success/error/pagination shapes, request validation wiring, an authorization seam, or security headers on its own.

1. A documented, reusable **standard success envelope for paged results** (`Page`, `PageSize`, `TotalCount`) exists in `SquadCrm.BuildingBlocks.Http`, matching the intake's Fields Dictionary exactly — no extra invented fields. Single-resource/normal success responses stay bare DTOs (D4) — no wrapper envelope is introduced for them.
2. Request validation stays centralized through the existing `ValidationEndpointFilter<T>` seam; this story documents it as the mandatory pattern, **attaches it to a real endpoint boundary** (it is currently attached to none), and proves it there against a second scenario (nested/multiple fields, via `[AsParameters]` binding) so "field-addressable" is demonstrated at a live boundary, not just unit-tested in isolation.
3. An **inert, scheme-agnostic authorization extension point** exists — `services.AddAuthorization()` registered with zero policies, and a documented `ICurrentUserAccessor` port in `SquadCrm.BuildingBlocks` narrowed to `IsAuthenticated` plus an opaque handle (D3) — so CRM-110 has exactly one place to plug in a real scheme, real policies and a real identity model. **No `UserId`/`OrganizationScope`/subject-kind discriminator, no default/anonymous implementation registered (fail-closed by design), no authentication scheme, no session, no login endpoint, no `[Authorize]` on any endpoint.**
4. Correlation IDs and safe exception mapping (already implemented by CRM-105) are verified consistent across the validation-problem and health-check response paths; `correlationId` and `traceId` are documented and tested as distinct concepts that legitimately diverge (D2), not as values that always match.
5. A small, environment-aware **security headers** middleware sits in `SquadCrm.BuildingBlocks`, applies a conventional low-risk header baseline, is registered in `Program.cs` immediately after `UseExceptionHandler()` (so error responses carry it too), and gates HSTS on the hosting environment rather than the individual request's scheme.
6. `docs/api-conventions.md` becomes the one place a module author reads to learn the shared success/error/pagination/validation/security/versioning contract (including the `code` error convention and the `/api/v1` route prefix), cross-linked from `src/backend/README.md`.

**Explicitly out of scope:** any authentication scheme, token/cookie/session implementation, login/logout endpoint (CRM-110); audit logging (CRM-114); domain/integration events and the transactional outbox (CRM-198); OpenTelemetry/structured-logging pipeline and deeper health checks beyond what CRM-105 ships (CRM-201); file storage (CRM-200); the full architecture-test suite build-out (CRM-202); any new business module, entity, DTO or endpoint beyond the minimum needed to prove these conventions; any PostgreSQL/EF Core change; any frontend change (Acceptance Criteria are backend/API-boundary scoped).

---

## Context — Read These Files First

1. `.squad/stories/shared-api-validation-security-foundation/CRM-204/intake.md` — the full intake. The **Fields Dictionary**, **Explicit non-goals** and **Open Questions** sections are hard constraints, not suggestions. The Open Questions (success-envelope shape for non-paginated responses, auth scheme choice, exact security-header set) are **not** resolved by this plan where no repository convention answers them — see the per-task notes below for what stays deliberately unresolved vs. what this plan decides.
2. `src/backend/src/Api/SquadCrm.Api/Program.cs` — **lines 12–17** (composition-root doc, configuration precedence), **lines 19–22** (Problem Details + exception handler + `AddHttpContextAccessor` — the accessor already registered is what `ICurrentUserAccessor` reuses), **lines 24–46** (the existing fail-fast CORS pattern — the security-headers registration follows the same "config-driven, explicit" shape), **lines 62–63** (`AddHealthChecks()`, liveness only — do not extend it), **lines 76–78** (`UseExceptionHandler()` → `UseMiddleware<CorrelationIdMiddleware>()` → `UseCors()` — the new `SecurityHeadersMiddleware` is added immediately after `UseCors()`, before `MapOpenApi`/`MapHealthChecks`/`MapModuleEndpoints`).
3. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Errors/GlobalExceptionHandler.cs` and `ProblemDetailsExtensions.cs` — full files (both ≤ 65 lines). `ProblemDetailsExtensions.TraceIdExtensionName` (`"traceId"`) and `ResolveTraceId` are the exact symbols the docs and tests reference; do not rename them. This story adds two more extension members here (Task 3a): `CodeExtensionName` (`"code"`) and `CorrelationIdExtensionName` (`"correlationId"`), the latter sourced from `CorrelationIdMiddleware`'s value, not from `ResolveTraceId`.
4. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Correlation/CorrelationIdMiddleware.cs` — full file. `HeaderName` (`"X-Correlation-Id"`) and `MaxLength` (128) are the symbols the doc references verbatim.
5. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Validation/ValidationEndpointFilter.cs` and `ValidationEndpointFilterExtensions.cs` — full files. Note the class doc on `ValidationEndpointFilter<TArgument>`: *"CRM-105 establishes the shape of validation only... deferred to the first stories that introduce real requests and endpoints."* This story is that deferred documentation/proof step — it must not replace `Validator`/DataAnnotations with a new validation library (FluentValidation is in the forbidden-prefix list, see Task 5).
6. `src/backend/src/Api/SquadCrm.Api/CorsOptions.cs` — full file (13 lines). `SectionName` (`"Cors"`) and `AllowedOrigins` are the pattern the new `SecurityHeadersOptions` (Task 4) follows: a small, `internal sealed` options class bound from a named configuration section with a safe empty/default value.
7. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Modules/IModule.cs` — full file (23 lines). `RegisterServices(IServiceCollection, IConfiguration)` is the seam a real future module uses; the authorization extension point (Task 3) must be composable the same way — registered once at the host, consumable by any module via standard DI, not smuggled into `IModule`.
8. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/ArchitectureFixtureModule.cs` — **lines 61–75** (`MapEndpoints`, the `ModuleInfoRoute` pattern: `TypedResults.Ok<T>`, `.WithName(...)`, `.WithSummary(...)`, `.WithDescription(...)`). The new paged-list proof endpoint (Task 2) follows this exact shape and stays in this same fixture module — **no new module is created**.
9. `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts/` — check the existing `ModuleInfoResponse` DTO location/namespace with `grep -rn "ModuleInfoResponse" src/backend/src/Modules/ArchitectureFixture` before adding the new paged-response DTO reference so it lands in the same contracts project, same namespace convention.
10. `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — **lines 22–45** (`ForbiddenAssemblyPrefixes`, whose doc comment at lines 24–27 names this exact story), **lines 128–147** (`Foundation_MustNotIntroduceForbiddenDependencies` — the rule this task updates). Only remove `"Microsoft.AspNetCore.Authorization."` from the array (Task 5); **leave `"Microsoft.AspNetCore.Authentication."` forbidden** — no authentication scheme is added here, only authorization service registration with zero policies.
11. `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` — read `All`, `IsModuleImplementation`, `ReferencedAssemblyNames` before writing any new architecture assertion; reuse these helpers, do not duplicate them.
12. `src/backend/tests/SquadCrm.Api.Tests/SquadCrmApiFactory.cs` — **lines 56–59** (`FaultRoute`, `SentinelMessage` constants), **lines 79–113** (constructor, `ConfigureWebHost`, the placeholder `POSTGRES_*` values and `PlaceholderPassword`), **lines 127–139** (fault injection). New tests for security headers and the paged-list endpoint use this same factory; do not create a second `WebApplicationFactory` subclass.
13. `src/backend/tests/SquadCrm.Api.Tests/ProblemDetailsTests.cs` — full file (≤ 50 lines). Matches the exact assertion style (`JsonDocument.Parse`, `root.GetProperty(...)`, `Assert.DoesNotContain` for leaked exception detail) the new validation and pagination tests must follow.
13a. **Correction:** `src/backend/tests/SquadCrm.Api.Tests/CorrelationIdTests.cs`, `CorsTests.cs`, `HealthEndpointTests.cs` and `ValidationFoundationTests.cs` **already exist**. Read each in full before touching it — this story **extends** these files, it does not create them. Only `PaginationTests.cs` and `SecurityHeadersTests.cs` are genuinely new.
14. `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj` — full file (19 lines). Confirms `SquadCrm.Api` already references `SquadCrm.BuildingBlocks`; no new project reference is needed for this story.
15. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/SquadCrm.BuildingBlocks.csproj` — full file (12 lines). `FrameworkReference Include="Microsoft.AspNetCore.App"` already covers `Microsoft.AspNetCore.Authorization` and `Microsoft.AspNetCore.Http.Abstractions` (for `ICurrentUserAccessor`'s use of `IHttpContextAccessor`) — **no new `PackageReference` is added to this project.**
16. `src/backend/README.md` — **lines 25–34** (Common commands table), **lines 49–68** (layout tree), **line 380** (the non-goals table row this story partially resolves — update the row to reflect the extension point now existing, while keeping CRM-110 as the owner of the real implementation). Confirm exact current line numbers with `grep -n "CRM-204" src/backend/README.md` before editing, since CRM-106's own edits may have shifted them.
17. `env/backend.env.example` — **lines 8–27**. No new operator-facing environment key is required for the authorization extension point (it takes no configuration). If `SecurityHeadersOptions` needs any environment-tunable value beyond a fixed conventional baseline, it must follow the existing `Section:Key` / `SECTION__KEY` double-underscore binding style shown at line 27 (`CORS__AllowedOrigins__0`) — do not invent a different binding style.

Grep hints while implementing:

- `grep -rn "AddAuthorization\|IAuthorizationService\|ICurrentUserAccessor" src/backend/src` — currently empty; confirms nothing pre-existing collides with the new names.
- `grep -rn "SecurityHeaders\|X-Content-Type-Options\|X-Frame-Options" src/backend` — currently empty.
- `grep -n "CRM-204" src/backend/README.md README.md src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — every placeholder this story is expected to resolve or partially resolve.

---

## Decisions this plan makes (record these; do not re-litigate during implementation)

| Decision | Choice | Rationale |
|---|---|---|
| Success envelope for **paged** results | New `PagedResult<T>` record in `SquadCrm.BuildingBlocks.Http` with exactly `Items` (`IReadOnlyList<T>`), `Page` (`int`), `PageSize` (`int`), `TotalCount` (`int`) | Matches the intake's Fields Dictionary verbatim. No `TotalPages`/`HasNextPage` — those are mechanical derivations an executor could add later without a contract change; inventing them now is unrequested scope. |
| `MaxPageSize` value and modifier | `200`, declared `public static readonly int MaxPageSize = 200;` on `PaginationRequest`, **not `const`** | `const` inlines the literal into every consumer assembly at compile time, so a future change would require recompiling every module; `static readonly` is resolved at runtime from `SquadCrm.BuildingBlocks` (approved implementation correction). |
| Namespace for the new HTTP building blocks | `SquadCrm.BuildingBlocks.Http` (pagination), not `SquadCrm.BuildingBlocks.Api` | Avoids a latent architecture-rule prefix trap where a future `Api`-prefixed rule could accidentally net this namespace (approved implementation correction). `SquadCrm.BuildingBlocks.Security` (authorization extension point + security headers) is unchanged. |
| Success envelope for **single-resource** responses (D4) | **Bare DTOs.** No wrapper envelope. Pagination uses `PagedResult<T>`; errors use RFC 9457. Cross-cutting response metadata travels in headers unless a future story explicitly designs otherwise. | Approved decision D4. Recorded as an explicit refinement of `docs/adr/ADR-003-api-errors.md` (the one ADR edit this plan authorizes, see Task 3c) — resolves what was previously an open intake question, rather than leaving it open. |
| Error `code` and localization key (D1) | RFC 9457 body gains a stable, machine-readable `code` extension member (`ProblemDetailsExtensions.CodeExtensionName = "code"`) plus a documented convention for deriving a localization key from it. The shared building block owns only the wire convention and plumbing — **no centralized mega-enum of every module's error codes.** Each module declares and owns its own codes under the shared naming convention (documented in `docs/api-conventions.md`), analogous to how each module owns its own DbContext/schema. | Approved decision D1, explicitly scoped MINIMAL per the user. Resolves the hardcoded-English-message finding: a message is now either localizable against its `code`, or explicitly recorded as developer-facing text keyed by code — not silently hardcoded English with no seam. |
| `correlationId` vs `traceId` (D2) | Kept as two **distinct** extension members. `traceId` (`ProblemDetailsExtensions.TraceIdExtensionName`, unchanged) stays the observability/tracing identifier (`Activity.Current?.Id ?? HttpContext.TraceIdentifier`). A new `correlationId` extension (`ProblemDetailsExtensions.CorrelationIdExtensionName = "correlationId"`) is sourced explicitly from `CorrelationIdMiddleware`'s echoed value, never from `ResolveTraceId`. Documented and tested as values that legitimately DIVERGE when an `Activity` is active — never as "always equal." | Approved decision D2. The original plan's "correlationId always matches traceId" convention was wrong once CRM-201 (OpenTelemetry) introduces a real `Activity`; asserting equality today would let CRM-201 silently break the contract later without a failing test to catch it. |
| Validation library | **Unchanged: `System.ComponentModel.DataAnnotations` via the existing `ValidationEndpointFilter<T>`.** No FluentValidation or other library added. | `ForbiddenAssemblyPrefixes` already forbids `FluentValidation`; CLAUDE.md and the intake require reuse, not replacement, of CRM-105's seam. |
| Where `ValidationEndpointFilter<T>` is proven | Attached to the fixture module's new paged-list endpoint (Task 2), binding `PaginationRequest` via `[AsParameters]` on a `GET`, with a boundary test asserting nested/multiple field errors through the live endpoint — not a direct `Validator.TryValidateObject` unit call. | Approved correction for B3: the filter was declared in CRM-105 but attached to zero endpoints; `[AsParameters]` is required because a reference-type parameter without it binds as a JSON body, which fails on `GET`. |
| Authorization extension point (narrowed, D3) | `services.AddAuthorization()` (zero policies) + a new `ICurrentUserAccessor` port narrowed to exactly `IsAuthenticated` (`bool`) and an opaque handle (`string? Handle`, or equivalent single opaque value — no `UserId`/`OrganizationScope` naming that implies an identity model) in `SquadCrm.BuildingBlocks.Security`, registered as scoped, backed by `IHttpContextAccessor`. **No default/anonymous implementation is registered.** A consumer resolving `ICurrentUserAccessor` before CRM-110 registers a real implementation gets a DI resolution failure, not a fabricated anonymous answer. | Approved decision D3. `AnonymousCurrentUserAccessor` is deleted outright — a plausible-looking anonymous default is worse than a loud fail-closed error, and CRM-110 owns the identity model (UserId, OrganizationScope, subject-kind discriminator) entirely; this story must not pre-guess its shape. |
| Authentication scheme | **None added.** `"Microsoft.AspNetCore.Authentication."` stays in `ForbiddenAssemblyPrefixes`. | Explicit non-goal; CRM-110 owns it. |
| Security headers | New `SecurityHeadersMiddleware` (hand-written, no third-party package) applying: `X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`, `X-Frame-Options: DENY`, and `Strict-Transport-Security: max-age=31536000; includeSubDomains` **only when `IHostEnvironment.IsProduction()` is true** (not `HttpContext.Request.IsHttps`) | Conventional, low-risk baseline; CSP/frame-ancestors deferred (open item, unchanged). Gating on the hosting environment rather than the individual request's scheme is an approved correction (B6): behind a TLS-terminating proxy, `Request.IsHttps` is `false` even in production, which would silently make HSTS never fire in production — the exact opposite of the intended behavior. Hand-written avoids adding a NuGet package for four static headers, consistent with CLAUDE.md's "avoid unnecessary packages" bias. |
| Security headers registration point | Immediately after `app.UseExceptionHandler()` (`Program.cs`, before `UseMiddleware<CorrelationIdMiddleware>()` and `UseCors()`) | Approved correction (B5): registering after `UseCors()` (the original plan) meant a `500` produced by the exception handler never received the headers. Registering first in the pipeline (right after the handler that can short-circuit into a response) guarantees every response, including error responses, carries the baseline. |
| Where the new types live | `SquadCrm.BuildingBlocks.Http` (pagination), `SquadCrm.BuildingBlocks.Security` (authorization extension point + security headers) — new folders inside the existing `SquadCrm.BuildingBlocks` project, mirroring `Errors/`, `Correlation/`, `Validation/` | No new project: these are technical cross-cutting concerns, exactly what `SquadCrm.BuildingBlocks` already hosts. |
| Proving the pagination contract | Extend the existing `ArchitectureFixture` module with one additional demo endpoint returning `PagedResult<ModuleInfoResponse>` (a trivial one-item page), routed under `/api/v1/...` (D5) | Mirrors CRM-106's precedent of proving a foundation contract through the existing non-business fixture rather than inventing a real endpoint. |
| API versioning route convention (D5) | Adopt `/api/v1` as the route prefix convention now, for the fixture demo endpoint(s). **Route-prefix + documentation only** — no version-negotiation framework, no `Asp.Versioning`/`Microsoft.AspNetCore.Mvc.Versioning` package, no content-negotiation or header-based versioning machinery. | Approved decision D5, explicitly scoped to avoid introducing broader versioning infrastructure this story has no mandate to design. |
| Architecture-rule update | Remove only `"Microsoft.AspNetCore.Authorization."` from `ForbiddenAssemblyPrefixes`; `SquadCrm.BuildingBlocks`'s existing assembly-level EF Core/Npgsql-free rule (`PersistenceArchitectureRulesTests.BuildingBlocks_MustNotDependOnEfCoreOrNpgsql`) already covers the new `Http`/`Security` folders — **no duplicate rule is added** (approved correction; confirmed by reading `PersistenceArchitectureRulesTests.cs` in Task 5) | Keeps the scope-creep guard meaningful — only the exact dependency this story legitimately introduces is unblocked — without adding a redundant assertion. |
| Documentation | New `docs/api-conventions.md`, cross-linked from `src/backend/README.md`; its canonical pagination/validation example is an **inline code sample**, not a link to the removable `ArchitectureFixture` route (approved correction) | ADR-003/ADR-004 state the *decision*; this story needs a *reusable reference* a module author actually reads day to day, distinct from the ADRs, that survives the fixture module's eventual deletion. |
| ADR-003 amendment (D4) | `docs/adr/ADR-003-api-errors.md` is amended to record the D4 refinement (bare DTOs for single-resource success, `PagedResult<T>` for pagination, RFC 9457 for errors, metadata in headers) — the **only** ADR edit this plan authorizes | Approved decision D4 explicitly directs this as an ADR refinement, not a silent contradiction of the "Story plans may refine details but must not silently contradict this ADR" rule in ADR-003 itself. `ADR-004-auth-authorization.md` remains untouched. |

---

## Backend Tasks

### 1 — Standard pagination envelope

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Http/PagedResult.cs`**

```csharp
namespace SquadCrm.BuildingBlocks.Http;

/// <summary>
/// Standard paged-result envelope for any module endpoint returning a page of
/// items. <see cref="Page"/> is 1-based; <see cref="TotalCount"/> is the total
/// number of items across all pages, not just this page's <see cref="Items"/>.
/// </summary>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
```

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Http/PaginationRequest.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace SquadCrm.BuildingBlocks.Http;

/// <summary>
/// Standard bounded pagination request. Bind this via <c>[AsParameters]</c> (it is
/// a reference type — binding it without <c>[AsParameters]</c> on a GET endpoint
/// makes minimal APIs treat it as a JSON body, which fails) on any module endpoint
/// accepting <c>page</c>/<c>pageSize</c> query parameters, so
/// <see cref="SquadCrm.BuildingBlocks.Validation.ValidationEndpointFilter{TArgument}"/>
/// enforces the bounds uniformly.
/// </summary>
public sealed class PaginationRequest
{
    /// <summary>
    /// <c>static readonly</c>, not <c>const</c>: a <c>const</c> would inline the
    /// literal into every consumer assembly at compile time instead of being
    /// resolved from this assembly at runtime.
    /// </summary>
    public static readonly int MaxPageSize = 200;

    [Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    public int Page { get; init; } = 1;

    [Range(1, 200, ErrorMessage = "PageSize must be between 1 and 200.")]
    public int PageSize { get; init; } = 20;
}
```

No changes to `SquadCrm.BuildingBlocks.csproj` — both files use only `System.*` types already available through the existing `FrameworkReference`.

### 2 — Prove the pagination envelope and attach `ValidationEndpointFilter<T>` at a real endpoint boundary

**File: `src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture/ArchitectureFixtureModule.cs`**

First run `grep -n "ModuleInfoResponse" src/backend/src/Modules/ArchitectureFixture/SquadCrm.Modules.ArchitectureFixture.Contracts -r` to confirm the DTO's exact namespace, then add, in `MapEndpoints` (after the existing `ModuleInfoRoute` mapping):

```csharp
endpoints.MapGet(ModuleInfoPageRoute, static ([AsParameters] PaginationRequest request, IModuleInfoProvider provider) =>
        TypedResults.Ok(new PagedResult<ModuleInfoResponse>(
            Items: [provider.Describe()],
            Page: request.Page,
            PageSize: request.PageSize,
            TotalCount: 1)))
    .WithName("ArchitectureFixtureModuleInfoPage")
    .WithSummary("Infrastructure/demo-only: proves the shared PagedResult<T> envelope and ValidationEndpointFilter<T> end to end.")
    .WithDescription(
        "Architecture scaffolding, not a CRM capability. Removed once a real business "
        + "module's paged endpoint provides equivalent coverage.")
    .ValidatesDataAnnotations<PaginationRequest>();
```

**This is the task that resolves the B3 correction:** `ValidationEndpointFilter<T>` was declared by CRM-105 but attached to zero endpoints. This endpoint is now the first real boundary that proves it — `PaginationRequest` is a reference type, so it must bind via `[AsParameters]`; without it, minimal APIs would try to bind the GET request as a JSON body and fail. The boundary test (Test Plan item 2, rewritten) sends this endpoint an out-of-range `page`/`pageSize` and asserts the resulting `HttpValidationProblemDetails.errors` dictionary carries both field keys — proving multiple/field-addressable errors through the live filter, not a direct `Validator.TryValidateObject` call.

Add a second route constant next to `ModuleInfoRoute` (line 39), under the `/api/v1` prefix (D5):

```csharp
public const string ModuleInfoPageRoute = "/api/v1/internal/architecture-fixture/module-info-page";
```

`ModuleInfoRoute` itself is unchanged — D5 scopes the `/api/v1` prefix adoption to this story's new endpoint; retrofitting the existing route is not required to prove the convention and would be unrequested churn.

Add `using SquadCrm.BuildingBlocks.Http;` and `using SquadCrm.BuildingBlocks.Validation;` to the file's using block.

### 3 — Authorization extension point (inert, narrowed per D3)

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Security/ICurrentUserAccessor.cs`**

```csharp
namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// The seam CRM-110 (authentication/session) implements for real. Deliberately
/// narrow: exposes only whether the caller is authenticated and an opaque
/// handle for that caller. It carries no identity model — no user id, no
/// organizational scope, no subject-kind discriminator (staff vs. customer).
/// CRM-110 owns designing that model; this port must not pre-guess its shape.
/// <para>
/// Authorization = Permission + Organizational Scope + Resource Ownership
/// (CLAUDE.md). None of the three are represented here — they are policy
/// concerns CRM-110 and later stories add on top of whatever identity model
/// CRM-110 designs.
/// </para>
/// <para>
/// <b>No default implementation is registered</b> for this interface in this
/// story. A consumer that resolves <see cref="ICurrentUserAccessor"/> before
/// CRM-110 registers a real implementation gets a DI resolution failure
/// (fail-closed), not a plausible-looking anonymous answer — a missing
/// registration must be loud, not silently "safe-by-coincidence."
/// </para>
/// </summary>
public interface ICurrentUserAccessor
{
    bool IsAuthenticated { get; }

    /// <summary>
    /// Opaque handle identifying the current caller when <see cref="IsAuthenticated"/>
    /// is <see langword="true"/>; <see langword="null"/> otherwise. Carries no
    /// meaning beyond "the same handle means the same caller" — CRM-110 defines
    /// what it actually contains.
    /// </summary>
    string? Handle { get; }
}
```

**Do not create `AnonymousCurrentUserAccessor`.** The original plan's default implementation is deleted from scope entirely (D3) — there is no file to create and no fallback behavior to implement.

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Security/SquadCrmAuthorizationExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// Registers the authorization extension point CRM-110 completes. Adds the
/// framework's authorization services with zero policies. Registers no
/// <see cref="ICurrentUserAccessor"/> implementation — CRM-110 is the first
/// story that registers one — and no authentication scheme, and maps no
/// <c>[Authorize]</c> endpoint. Any code that resolves
/// <see cref="ICurrentUserAccessor"/> before CRM-110 lands will fail DI
/// resolution loudly; that is intentional (fail-closed, see the interface's
/// doc comment).
/// </summary>
public static class SquadCrmAuthorizationExtensions
{
    public static IServiceCollection AddSquadCrmAuthorizationExtensionPoint(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddAuthorization();

        return services;
    }
}
```

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`** — after line 22 (`builder.Services.AddHttpContextAccessor();`), add:

```csharp
// Authorization extension point for CRM-110. No scheme, no policy, no
// [Authorize] endpoint, no ICurrentUserAccessor registration yet — this only
// gives CRM-110 one place to plug in. Resolving ICurrentUserAccessor before
// CRM-110 registers an implementation fails DI resolution by design.
builder.Services.AddSquadCrmAuthorizationExtensionPoint();
```

Add `using SquadCrm.BuildingBlocks.Security;` to the top imports (after line 7). **Do not add `app.UseAuthentication()` or `app.UseAuthorization()` to the pipeline** — there is no scheme to authenticate against and no endpoint requires authorization; adding either now is dead middleware that CRM-110 adds when it has something to protect.

Add a test (Test Plan item, new) proving the fail-closed behavior: build a `ServiceProvider` from `AddSquadCrmAuthorizationExtensionPoint()` alone and assert `GetRequiredService<ICurrentUserAccessor>()` throws `InvalidOperationException` — the DI container's standard missing-registration failure — rather than resolving to any implementation.

### 4 — Environment-aware security headers

**Create file: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Security/SecurityHeadersMiddleware.cs`**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// Adds a conventional, low-risk response header baseline to every response,
/// including error responses (registered right after
/// <c>app.UseExceptionHandler()</c> — see <c>Program.cs</c> — so a 500 carries
/// the headers too).
/// <para>
/// <see cref="StrictTransportSecurityValue"/> is added only when
/// <see cref="IHostEnvironment.IsProduction"/> is <see langword="true"/> —
/// deliberately <b>not</b> gated on <c>HttpContext.Request.IsHttps</c>. Behind
/// a TLS-terminating proxy (the expected production topology) the inbound
/// request to Kestrel is plain HTTP even though the client connection was
/// HTTPS, so <c>Request.IsHttps</c> would be <see langword="false"/> and HSTS
/// would silently never fire in production if gated that way.
/// </para>
/// <para>
/// Deliberately excluded: <c>Content-Security-Policy</c> and
/// <c>frame-ancestors</c> — their correct value depends on the not-yet-built
/// frontend/deployment topology (open item; see the CRM-204 intake).
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string StrictTransportSecurityValue = "max-age=31536000; includeSubDomains";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(environment);
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool isProduction = _environment.IsProduction();

        context.Response.OnStarting(state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            if (isProduction)
            {
                ctx.Response.Headers["Strict-Transport-Security"] = StrictTransportSecurityValue;
            }

            return Task.CompletedTask;
        }, context);

        await _next(context).ConfigureAwait(false);
    }
}
```

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`** — immediately after line 76 (`app.UseExceptionHandler();`), **before** `app.UseMiddleware<CorrelationIdMiddleware>();` and `app.UseCors();`, add:

```csharp
// Registered first, right after the exception handler, so 500 responses
// carry the header baseline too — not after UseCors(), which would skip them.
app.UseMiddleware<SecurityHeadersMiddleware>();
```

No configuration section is added: the header set is a fixed conventional baseline (see the Decisions table), not environment-tunable, so there is nothing to bind and no new `env/backend.env.example` key.

### 5 — Update the architecture-rule scope-creep guard

**File: `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`**

In `ForbiddenAssemblyPrefixes` (lines 35–45), remove exactly this line:

```csharp
"Microsoft.AspNetCore.Authorization.",
```

Leave `"Microsoft.AspNetCore.Authentication."` in place. Update the doc comment at lines 24–27 to state this story resolved the `Authorization` half and CRM-110 still owns `Authentication`.

**Confirmed (approved correction): no new persistence-boundary rule is needed.** `PersistenceArchitectureRulesTests.BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` (`src/backend/tests/SquadCrm.ArchitectureTests/PersistenceArchitectureRulesTests.cs`, currently around line 60) already asserts `SquadCrm.BuildingBlocks`'s referenced assemblies carry no `Microsoft.EntityFrameworkCore`/`Npgsql` prefix at the whole-assembly level via `SquadCrmAssemblies.BuildingBlocks` — this already covers the new `Http/` and `Security/` folders, since they live inside the same assembly. Add **no duplicate rule**; add a one-line code comment at that existing test noting it already covers CRM-204's new folders, so a future reader does not "helpfully" duplicate it.

### 6 — Error `code`, localization key and `correlationId` extension (D1, D2)

**File: `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Errors/ProblemDetailsExtensions.cs`**

Add two extension-member name constants next to the existing `TraceIdExtensionName`, and populate both from `CustomizeProblemDetails`:

```csharp
/// <summary>
/// Name of the stable, machine-readable error-code extension member. Always
/// lowercase <c>code</c>. Each module declares and owns its own code values
/// under its own naming convention (documented in docs/api-conventions.md);
/// this building block owns only the wire format and plumbing — it is not a
/// registry of every module's codes.
/// </summary>
public const string CodeExtensionName = "code";

/// <summary>
/// Name of the client/support correlation-handle extension member. Always
/// lowercase <c>correlationId</c>. Sourced from <c>CorrelationIdMiddleware</c>'s
/// echoed value — <b>distinct from <see cref="TraceIdExtensionName"/></b>, which
/// is the observability/tracing identifier. The two are never guaranteed equal;
/// they diverge whenever an <see cref="System.Diagnostics.Activity"/> is active.
/// </summary>
public const string CorrelationIdExtensionName = "correlationId";
```

`code` is populated only where a handler/filter explicitly sets it via `context.ProblemDetails.Extensions[CodeExtensionName]` (e.g. `GlobalExceptionHandler` sets a fixed generic code such as `"unexpected-error"`; `ValidationEndpointFilter<T>`'s `HttpValidationProblemDetails` path is documented as the field-level `errors` shape and does not require a top-level `code` for validation failures — document this split explicitly in Task 8, do not invent a code for every validation error here). `correlationId` is populated unconditionally in `CustomizeProblemDetails`, read from `context.HttpContext.Response.Headers[CorrelationIdMiddleware.HeaderName]` (the value `CorrelationIdMiddleware` already wrote via `OnStarting`) or, if not yet written at customization time, from `context.HttpContext.TraceIdentifier` (the same value `CorrelationIdMiddleware` promoted from the sanitised inbound/generated correlation id) — confirm the exact read point by checking whether `OnStarting` has fired before `CustomizeProblemDetails` runs for the given response path, and use whichever is reliably populated; document the chosen read point with a code comment so the "distinct from traceId" invariant is visibly enforced, not just assumed.

**Naming convention for module-owned codes (document in Task 8, `docs/api-conventions.md`):** a module's own error codes are short, kebab-case, module-prefixed strings (e.g. `contacts.duplicate-email`), each individually owned and versioned by that module — this plan does not enumerate or reserve any module-specific code. No shared mega-enum, no shared registry type, no central switch statement mapping codes anywhere in `SquadCrm.BuildingBlocks`.

### 7 — Amend ADR-003 to record the D4 success-response refinement (the one authorized ADR edit)

**File: `docs/adr/ADR-003-api-errors.md`**

Add a dated addendum section (do not rewrite the existing "Decision"/"Rule" sections) recording:

- Single-resource / normal success responses are bare DTOs — no wrapper envelope.
- Paginated success responses use `PagedResult<T>` (`SquadCrm.BuildingBlocks.Http`).
- Errors use RFC 9457 Problem Details, now carrying `traceId`, `correlationId` and `code` extension members (this story, CRM-204).
- Cross-cutting response metadata (anything not part of the resource itself) travels in response headers unless a future story explicitly designs a different mechanism.

This is the **only** ADR edit this plan authorizes (D4). `docs/adr/ADR-004-auth-authorization.md` is unchanged — the narrowed `ICurrentUserAccessor` (Task 3) is a plan-level implementation detail within ADR-004's existing "keep identity semantics distinct" direction, not a new architectural decision requiring its own ADR edit.

### 8 — Shared API conventions documentation

**Create file: `docs/api-conventions.md`**

Document, with concrete symbol references (not paraphrases), using **inline code samples as the canonical examples** (not a link to the removable `ArchitectureFixture` fixture route — approved correction):

- The RFC 9457 error contract: `type`, `title`, `status`, `instance`, lowercase `traceId` (`ProblemDetailsExtensions.TraceIdExtensionName`), lowercase `code` (`ProblemDetailsExtensions.CodeExtensionName`) and lowercase `correlationId` (`ProblemDetailsExtensions.CorrelationIdExtensionName`), populated by `AddSquadCrmProblemDetails()` and `GlobalExceptionHandler`. Field-addressable validation errors via `HttpValidationProblemDetails.errors` (produced by `ValidationEndpointFilter<T>`).
- The `code`/localization-key convention (D1): the shared wire convention and plumbing only; each module declares and owns its own codes; no central registry. Include the naming convention from Task 6 and one worked inline example (request → 4xx body with a `code`).
- **`correlationId` vs. `traceId` (D2), explicit and prominent:** these are DIFFERENT concepts. `correlationId` is the stable client/support handle (`X-Correlation-Id` request/response header, `CorrelationIdMiddleware.HeaderName`, sanitisation rules — 128-char max, no control characters). `traceId` is the observability/tracing identifier. **They are never guaranteed equal and must not be tested or documented as always matching** — they diverge once an `Activity` is active (e.g. under CRM-201's future OpenTelemetry wiring).
- The pagination envelope: `PagedResult<T>` (`Items`, `Page`, `PageSize`, `TotalCount`) and `PaginationRequest` (`Page` >= 1, `PageSize` 1–200, `MaxPageSize` = 200), with an **inline worked code sample** (request/response JSON), not a link to the fixture route. Note explicitly: a mandatory non-nullable `TotalCount` forces a `COUNT(*)` on any real paged query; CRM-114 (audit) may need a cursor/keyset variant of pagination instead — record this as a known future extension point of the convention, not a surprise a later story has to rediscover.
- Success responses (D4): bare DTOs for single-resource/normal responses, `PagedResult<T>` for pagination, RFC 9457 for errors; cross-cutting metadata travels in headers unless explicitly redesigned. Cross-reference the ADR-003 addendum from Task 7.
- API versioning (D5): `/api/v1` route-prefix convention, adopted now for new endpoints. Explicit note: this is a route-prefix convention only — no version-negotiation framework, no content-negotiation machinery, introduced by this story.
- The authorization extension point: narrowed `ICurrentUserAccessor` (`IsAuthenticated` + opaque `Handle` only), `AddSquadCrmAuthorizationExtensionPoint()`, the explicit fail-closed behavior (no default registration — DI resolution fails until CRM-110 registers a real implementation), and an explicit note that **no endpoint is protected yet** — this is where CRM-110 plugs in.
- The security-header baseline (Task 4's four headers), that HSTS is gated on `IHostEnvironment.IsProduction()` (not request scheme), and that CSP/`frame-ancestors` are explicitly deferred, not decided.
- CORS: config-bound `Cors:AllowedOrigins`, fail-fast on `*`.
- A short "what's explicitly NOT here yet" list mirroring the plan's non-goals, each with its owning story id.

**File: `src/backend/README.md`** — after confirming current line numbers with `grep -n "CRM-204\|## Layout\|## Common commands" src/backend/README.md`:
- Add a row to the Common commands table (around lines 25–34) is not needed (no new CLI command). Instead add one sentence under the existing relevant section pointing to `docs/api-conventions.md` for the shared API/validation/security contract.
- Update the non-goals table row (around line 380) from `| Authentication / authorization / sessions | CRM-204, CRM-110 |` to `| Authentication / session implementation (extension point ready) | CRM-110 |`, keeping the row's table shape intact.

---

## Edge Cases & Failure Modes

- **`PageSize` above `PaginationRequest.MaxPageSize` (200) or `Page` below 1, at the live fixture endpoint** — rejected by `ValidationEndpointFilter<T>` via `.ValidatesDataAnnotations<PaginationRequest>()` (Task 2) bound with `[AsParameters]`, producing the same `HttpValidationProblemDetails` shape as any other validation failure — not a bespoke pagination error shape. Covered by Test Plan item 2 (rewritten as a boundary test).
- **`TotalCount` of `0` with `Page` requested beyond the last page** — this story does not implement a real paged query, so no clamping/short-circuit logic is added; the fixture proof always returns exactly one item. Document in `docs/api-conventions.md` that clamping/out-of-range behaviour, and the fact that a mandatory non-nullable `TotalCount` forces `COUNT(*)` on a real query (with a future cursor/keyset variant likely needed by CRM-114), are the consuming module's responsibility — not something `PagedResult<T>` itself enforces.
- **Request without a client-supplied `X-Correlation-Id`** — already handled by `CorrelationIdMiddleware.Generate()` (existing code, unchanged); verify (Test Plan item 4) that the generated correlation id appears as `correlationId` on a validation-problem response, and separately verify `correlationId` and `traceId` DIVERGE once an `Activity` is active — never that they're equal.
- **Plain-HTTP request in local/staging without a terminating proxy, but `IHostEnvironment` is Production** — `SecurityHeadersMiddleware` still emits `Strict-Transport-Security` under this gate (Task 4, D correction B6), because the gate is the hosting environment, not the individual request's scheme; this is intentional — a production deployment without TLS termination is a deployment misconfiguration `SecurityHeadersMiddleware` cannot detect, not something the middleware should silently work around.
- **Plain-HTTP request in `Development`** — `Strict-Transport-Security` is correctly omitted (`IHostEnvironment.IsProduction()` is `false`), so local `http://localhost` development is unaffected. Covered by Test Plan item 3.
- **A 500 produced by `GlobalExceptionHandler`** — must still carry the security-header baseline, because `SecurityHeadersMiddleware` is registered immediately after `UseExceptionHandler()` (Task 4, correction B5), ahead of where the exception handler short-circuits the pipeline. Covered by Test Plan item 3a (new).
- **A future module resolves `ICurrentUserAccessor` before CRM-110 registers a real implementation (D3)** — no default/anonymous implementation exists; DI resolution throws `InvalidOperationException` (fail-closed), so a module written against the seam before CRM-110 lands fails loudly at startup/first-resolution rather than silently behaving as if every caller is anonymous. Covered by Test Plan item 4a (new). Document this explicitly (Task 8) so no later story mistakes "registered" for "enforced," and so nobody re-adds a default implementation to "fix" the DI error without realizing it was deliberate.
- **`app.UseAuthorization()` accidentally added later without a scheme** — would throw at first `[Authorize]`-protected request with no configured `DefaultAuthenticateScheme`. This story deliberately does **not** call `UseAuthentication()`/`UseAuthorization()` in the pipeline (Task 3) specifically to avoid this trap; the code comment at the registration site must say why, so a later story does not "helpfully" add it without also adding a scheme.
- **A module sets a `code` extension value that collides with another module's** — out of this story's control by design (D1): no shared registry exists to prevent it. Document in `docs/api-conventions.md` that the module-prefix convention (e.g. `contacts.duplicate-email`) is the only collision-avoidance mechanism, and collisions across modules are each module owner's responsibility, not something `SquadCrm.BuildingBlocks` polices.

---

## Test Plan

1. **`src/backend/tests/SquadCrm.Api.Tests/PaginationTests.cs` (new)** — `GET /api/v1/internal/architecture-fixture/module-info-page` returns `200`, and the JSON body has `items` (array, length 1), `page` = 1, `pageSize` = 1, `totalCount` = 1 — matching `ProblemDetailsTests`' `JsonDocument.Parse` / `root.GetProperty(...)` style, using `SquadCrmApiFactory`.
2. **`src/backend/tests/SquadCrm.Api.Tests/PaginationTests.cs`** — a **boundary test through the live endpoint** (not a direct `Validator.TryValidateObject` call): `GET /api/v1/internal/architecture-fixture/module-info-page?page=0&pageSize=500` via `SquadCrmApiFactory`, asserting a `400` `HttpValidationProblemDetails` body whose `errors` dictionary contains **both** `page` and `pageSize` keys — proving `[AsParameters]` binding, `ValidationEndpointFilter<T>` attachment and multiple/nested field-addressable errors all work together at a real boundary (resolves B3).
3. **`src/backend/tests/SquadCrm.Api.Tests/SecurityHeadersTests.cs` (new)** — a `GET /health` request via `SquadCrmApiFactory` (Development and Production environments, mirroring `ProblemDetailsTests`' `[InlineData]` pattern) asserts `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin` are always present; `Strict-Transport-Security` is **absent** in Development and **present** in Production (proves the `IHostEnvironment.IsProduction()` gate, resolves B6 — the original plan's "absent because not HTTPS" assertion is replaced).
3a. **`src/backend/tests/SquadCrm.Api.Tests/SecurityHeadersTests.cs`** — a request to the existing fault route (triggers `GlobalExceptionHandler`, a `500`) via `SquadCrmApiFactory` asserts the same header baseline is present on the `500` response (resolves B5).
4. **`src/backend/tests/SquadCrm.Api.Tests/CorrelationIdTests.cs` (extend the existing file — do not create)** — send a request with no `X-Correlation-Id` header to a route that fails validation, assert the response's `X-Correlation-Id` header value equals the response body's `correlationId` field. **Separately**, assert `correlationId` and `traceId` DIVERGE when an ambient `Activity` is started for the request (e.g. wrap the request in `using var activity = new Activity("test").Start();` or the equivalent test-harness mechanism) — this is the D2 test replacing the removed "always matches" assertion, and it exists specifically so CRM-201 (OpenTelemetry) cannot silently collapse the two back into equality without a failing test.
4a. **`src/backend/tests/SquadCrm.Api.Tests/` (new, e.g. `CurrentUserAccessorTests.cs`)** — build a `ServiceProvider` from a service collection that only calls `AddSquadCrmAuthorizationExtensionPoint()`, and assert `serviceProvider.GetRequiredService<ICurrentUserAccessor>()` throws `InvalidOperationException` — proves the fail-closed D3 behavior (no default registration).
5. **`src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs`** — extend or add a fact asserting `Foundation_MustNotIntroduceForbiddenDependencies` still passes with `Microsoft.AspNetCore.Authorization.` removed from the list (the test suite itself is the proof the removal was scoped correctly — no other assembly newly references it). **No new persistence-boundary rule is added** (Task 5 confirms `PersistenceArchitectureRulesTests.BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` already covers the new folders).
6. Run the full backend suite to confirm no regression: `dotnet test tests/SquadCrm.Api.Tests` and `dotnet test tests/SquadCrm.ArchitectureTests` (both require no database, per `src/backend/README.md` line 32–33).

---

## Verification Steps

1. **Backend builds:** `cd src/backend && dotnet build` — must succeed with `TreatWarningsAsErrors=true` unchanged (`Directory.Build.props`), no new suppression.
2. **Architecture tests:** `cd src/backend && dotnet test tests/SquadCrm.ArchitectureTests` — no database needed; must pass, proving `Foundation_MustNotIntroduceForbiddenDependencies` still holds with the narrowed `ForbiddenAssemblyPrefixes` and that `BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` still passes with the new `Http`/`Security` folders in place.
3. **API tests:** `cd src/backend && dotnet test tests/SquadCrm.Api.Tests` — no database needed; must include the new/extended pagination (incl. boundary/`[AsParameters]`), security-header (incl. 500-response and Production-HSTS), correlation (incl. divergence), and fail-closed `ICurrentUserAccessor` tests all passing, and the pre-existing `ProblemDetailsTests`, `CorsTests`, `HealthEndpointTests`, `ValidationFoundationTests` unchanged/extended and passing.
4. **Full suite (optional, requires PostgreSQL):** `docker compose up -d && cd src/backend && dotnet test` — confirms no regression to `SquadCrm.Persistence.IntegrationTests`, which this story does not touch.
5. **Manual smoke:** `cd src/backend && dotnet run --project src/Api/SquadCrm.Api`, then `curl -i http://localhost:5080/api/v1/internal/architecture-fixture/module-info-page` (expect the `PagedResult<T>` JSON shape) and `curl -i http://localhost:5080/health` (expect `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` present; `Strict-Transport-Security` absent in Development).

---

## Done Criteria

- [ ] `PagedResult<T>` and `PaginationRequest` exist in `SquadCrm.BuildingBlocks.Http` (not `.Api`), `MaxPageSize` is `static readonly int` = 200, matching the intake's `Page`/`PageSize`/`TotalCount` fields exactly, and are proven end to end by the fixture module's new `/api/v1` paged endpoint. (AC: "Standard success/pagination/error conventions are documented and reusable.")
- [ ] `ValidationEndpointFilter<T>` is attached to the fixture module's paged endpoint via `.ValidatesDataAnnotations<PaginationRequest>()` with `[AsParameters]` binding, and a boundary test proves multiple/field-addressable errors through that live endpoint. No new validation library added. (AC: "Request validation is centralized at application boundaries.")
- [ ] RFC 9457 error bodies carry `traceId` (unchanged), a new stable `code` extension member (module-owned values, no central registry — D1), and a new `correlationId` extension member sourced from `CorrelationIdMiddleware` (D2). `docs/api-conventions.md` documents the `code` naming convention and localization-key derivation, and explicitly documents `correlationId`/`traceId` as distinct, divergence-tested concepts. (AC: "Standard success/pagination/error conventions are documented and reusable"; "Correlation IDs and safe exception mapping are applied consistently.")
- [ ] `docs/adr/ADR-003-api-errors.md` carries the D4 addendum (bare DTOs for single-resource success, `PagedResult<T>` for pagination, RFC 9457 for errors, metadata in headers) — the only ADR edit this plan authorizes; `ADR-004-auth-authorization.md` is untouched.
- [ ] `docs/api-conventions.md` documents the success/pagination/error/correlation/security/versioning conventions with concrete symbol references and inline code samples (not the removable fixture route), cross-linked from `src/backend/README.md`.
- [ ] Narrowed `ICurrentUserAccessor` (`IsAuthenticated` + opaque `Handle` only, no `UserId`/`OrganizationScope`) + `AddSquadCrmAuthorizationExtensionPoint()` exist, are registered in `Program.cs`; `AnonymousCurrentUserAccessor` does not exist and no default implementation is registered — a test proves `GetRequiredService<ICurrentUserAccessor>()` throws before CRM-110 registers a real implementation (D3). No authentication scheme, session, login endpoint or `[Authorize]`-protected endpoint exists anywhere in the diff. (AC: "Authentication/authorization extension points are prepared for Sprint 1.")
- [ ] `correlationId`/`traceId` distinctness is proven by a test asserting they diverge under an active `Activity`, replacing any "always matches" assertion. Correlation-header-to-body-field consistency is proven across both the unhandled-exception path (pre-existing) and the validation-problem path (new/extended test). (AC: "Correlation IDs and safe exception mapping are applied consistently.")
- [ ] `SecurityHeadersMiddleware` adds the four documented headers on every response including `500`s, registered in `Program.cs` immediately after `UseExceptionHandler()` (not after `UseCors()`); HSTS is gated on `IHostEnvironment.IsProduction()`, present in Production and absent in Development, proven by tests in both environments; existing environment-aware CORS fail-fast-on-wildcard behaviour is unchanged. (AC: "Security headers/CORS/configuration follow environment-aware defaults.")
- [ ] `/api/v1` route-prefix convention is adopted by the fixture module's new demo endpoint and documented; no version-negotiation framework or broader versioning machinery was introduced (D5).
- [ ] `ArchitectureRulesTests.ForbiddenAssemblyPrefixes` no longer forbids `Microsoft.AspNetCore.Authorization.`, still forbids `Microsoft.AspNetCore.Authentication.`, and all architecture tests pass; no duplicate EF Core/Npgsql-free rule was added (the existing `PersistenceArchitectureRulesTests.BuildingBlocks_MustNotDependOnEfCoreOrNpgsql` already covers it).
- [ ] No code for CRM-110, CRM-114, CRM-198, CRM-200, CRM-201 or CRM-202 was added; no new business module, entity or persistence code was added.
- [ ] `dotnet build`, `dotnet test tests/SquadCrm.ArchitectureTests` and `dotnet test tests/SquadCrm.Api.Tests` all pass from `src/backend/`.
