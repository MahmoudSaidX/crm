# API Conventions — Squad CRM

The one place a module author reads to learn the shared success/error/
pagination/validation/security/versioning contract every module endpoint
follows. Refines `docs/adr/ADR-003-api-errors.md` (see its 2026-08-26
addendum) and prepares the extension point `docs/adr/ADR-004-auth-authorization.md`
requires; it does not replace either ADR.

## Success responses (D4)

- **Single-resource / normal success:** bare DTOs. No wrapper envelope.
- **Paginated success:** `PagedResult<T>` (`SquadCrm.BuildingBlocks.Http`).
- **Errors:** RFC 9457 Problem Details (below).
- **Cross-cutting metadata** (anything not part of the resource itself —
  e.g. a correlation id) travels in response **headers**, unless a future
  story explicitly designs a different mechanism.

## Error contract — RFC 9457 Problem Details

Every error response is shaped by `AddSquadCrmProblemDetails()`
(`SquadCrm.BuildingBlocks.Errors.ProblemDetailsExtensions`) and, for
unhandled exceptions, `GlobalExceptionHandler`. Every error body carries:

- `type`, `title`, `status`, `instance` — standard RFC 9457 members.
- `traceId` (`ProblemDetailsExtensions.TraceIdExtensionName`, lowercase) —
  the observability/tracing identifier.
- `correlationId` (`ProblemDetailsExtensions.CorrelationIdExtensionName`,
  lowercase) — the stable client/support handle.
- `code` (`ProblemDetailsExtensions.CodeExtensionName`, lowercase) — a
  stable, machine-readable error code, populated only where a
  handler/filter explicitly sets it.

Field-addressable **validation** errors use
`HttpValidationProblemDetails.errors` (an `errors` dictionary keyed by
field name), produced by `ValidationEndpointFilter<T>` — see
[Request validation](#request-validation-validationendpointfiltert) below.
Validation failures do not carry a top-level `code`; the `errors`
dictionary is the field-level detail.

### `correlationId` vs. `traceId` — distinct, not interchangeable (D2)

These are **different concepts** and must never be documented, tested, or
relied upon as equal:

- **`correlationId`** — the stable client/support handle. Sourced from the
  `X-Correlation-Id` request/response header
  (`CorrelationIdMiddleware.HeaderName`). A client-supplied value is
  sanitised: empty, longer than `CorrelationIdMiddleware.MaxLength` (128
  characters), or containing control characters are all replaced by a
  freshly generated value. The middleware promotes the sanitised value onto
  `HttpContext.TraceIdentifier` and echoes it on the response header; the
  `correlationId` extension member reads that same promoted value.
- **`traceId`** — the observability/tracing identifier
  (`ProblemDetailsExtensions.ResolveTraceId`): the ambient
  `System.Diagnostics.Activity.Current?.Id` when one exists, otherwise the
  same `HttpContext.TraceIdentifier` value.

**They are never guaranteed equal.** With no ambient `Activity` (today's
default, before CRM-201's OpenTelemetry wiring), `traceId` happens to equal
`correlationId` because both fall back to the same `TraceIdentifier`. Once
an `Activity` is active, `traceId` reports the `Activity`'s id while
`correlationId` keeps reporting the stable client handle — they legitimately
**diverge**. Do not add or restore an "always matches" assertion; test and
document divergence instead.

### `code` and the localization key (D1)

The `code` extension is a **minimal, shared wire convention and plumbing
only** — `SquadCrm.BuildingBlocks` does not maintain a centralized
mega-enum or registry of every module's error codes. Each
capability/module declares and owns its own codes.

**Naming convention:** short, kebab-case, module-prefixed strings, e.g.
`contacts.duplicate-email`. Each module owns and versions its own codes;
cross-module collisions are not detected or prevented by
`SquadCrm.BuildingBlocks` — the module-prefix convention is the only
collision-avoidance mechanism, and avoiding a collision is each module
owner's responsibility.

**Localization key:** derive a module's localization key directly from its
`code` (e.g. the key `errors.contacts.duplicate-email` for the code
`contacts.duplicate-email`). A message is either localizable against its
`code` this way, or is explicitly recorded as developer-facing text keyed
by `code` — never silently hardcoded English with no seam.

**Worked example** — a module rejecting a duplicate email might set:

```csharp
context.ProblemDetails.Extensions[ProblemDetailsExtensions.CodeExtensionName] = "contacts.duplicate-email";
```

producing a `4xx` body such as:

```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
  "title": "A contact with this email already exists.",
  "status": 409,
  "instance": "/api/v1/contacts",
  "traceId": "0dfd9c...",
  "correlationId": "abc-123-def",
  "code": "contacts.duplicate-email"
}
```

`GlobalExceptionHandler` sets a fixed generic code, `"unexpected-error"`,
for every unhandled exception — it is infrastructure-owned, not
module-owned, because the handler runs before any module-specific error is
distinguishable.

## Pagination — `PagedResult<T>` and `PaginationRequest`

`SquadCrm.BuildingBlocks.Http`:

```csharp
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed class PaginationRequest
{
    public static readonly int MaxPageSize = 200;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 200)]
    public int PageSize { get; init; } = 20;
}
```

- `Page` is 1-based, minimum 1.
- `PageSize` is bounded 1–`PaginationRequest.MaxPageSize` (200).
- `TotalCount` is the total number of items across **all** pages, not just
  the current page's `Items`.
- No `TotalPages`/`HasNextPage` — those are mechanical derivations a
  consumer can add without a contract change.

Bind `PaginationRequest` via `[AsParameters]` on a `GET` endpoint — it is a
reference type, so binding it without `[AsParameters]` makes minimal APIs
treat it as a JSON request body, which fails on `GET`. Attach
`.ValidatesDataAnnotations<PaginationRequest>()` so out-of-range values
produce the standard `HttpValidationProblemDetails` shape, not a bespoke
pagination error.

**Worked example**, proven end to end by
`ArchitectureFixtureModule.ModuleInfoPageRoute`
(`/api/v1/internal/architecture-fixture/module-info-page`):

```csharp
endpoints.MapGet(route, static ([AsParameters] PaginationRequest request, IProvider provider) =>
        TypedResults.Ok(new PagedResult<TItem>(
            Items: provider.GetPage(request.Page, request.PageSize),
            Page: request.Page,
            PageSize: request.PageSize,
            TotalCount: provider.CountAll())))
    .ValidatesDataAnnotations<PaginationRequest>();
```

Request: `GET /api/v1/.../items?page=1&pageSize=20` →

```json
{ "items": [ /* ... */ ], "page": 1, "pageSize": 20, "totalCount": 137 }
```

Request: `GET /api/v1/.../items?page=0&pageSize=500` → `400`
`HttpValidationProblemDetails` with `errors.page` and `errors.pageSize`
both populated.

**Known future extension point:** a mandatory, non-nullable `TotalCount`
forces a `COUNT(*)` on any real paged query. A high-volume module (e.g.
CRM-114's audit log) may need a cursor/keyset pagination variant instead of
offset pagination — that is a known, deliberate future extension of this
convention, not something `PagedResult<T>` itself prevents or a surprise a
later story should have to rediscover.

`PagedResult<T>` does not clamp or short-circuit an out-of-range `Page`
against `TotalCount`; that behaviour, like the underlying query itself, is
the consuming module's responsibility.

## Request validation — `ValidationEndpointFilter<T>`

The mandatory, centralized validation seam for every module endpoint.
Built on `System.ComponentModel.DataAnnotations` — **no FluentValidation or
other validation library** is added
(`Foundation_MustNotIntroduceForbiddenDependencies` forbids the
`FluentValidation` prefix).

```csharp
endpoints.MapPost(route, static (MyRequest request) => ...)
    .ValidatesDataAnnotations<MyRequest>();
```

A failing request produces a `400` `HttpValidationProblemDetails` body
whose `errors` dictionary is keyed by field/member name — the same
field-addressable shape for every module, proven at a live boundary by
`ArchitectureFixtureModule`'s paged endpoint (nested/multiple `page` and
`pageSize` errors through `[AsParameters]` binding).

## Authorization extension point (D3)

`SquadCrm.BuildingBlocks.Security`:

- `ICurrentUserAccessor` — narrowed to exactly `IsAuthenticated` (`bool`)
  and an opaque `Handle` (`string?`). **No `UserId`, no
  `OrganizationScope`, no subject-kind discriminator** — CRM-110 owns
  designing that identity model; this port does not pre-guess its shape.
- `AddSquadCrmAuthorizationExtensionPoint()` — registers
  `services.AddAuthorization()` with **zero policies**. Registered in
  `Program.cs`.

**No default/anonymous implementation of `ICurrentUserAccessor` is
registered.** A consumer that resolves it before CRM-110 registers a real
implementation gets a DI resolution failure (`InvalidOperationException`),
not a plausible-looking anonymous answer — fail-closed by design. Do not
"fix" that failure by adding a default implementation; it is deliberate.

**No endpoint is protected yet.** No authentication scheme, no session, no
login endpoint, and no `[Authorize]` attribute exist anywhere in this
story's diff — `app.UseAuthentication()`/`app.UseAuthorization()` are
deliberately not added to the pipeline either, since there is no scheme to
authenticate against yet. This is exactly where CRM-110 plugs in.

## Security headers

`SecurityHeadersMiddleware` (`SquadCrm.BuildingBlocks.Security`),
registered in `Program.cs` **immediately after `app.UseExceptionHandler()`**
(before `CorrelationIdMiddleware` and `UseCors()`) so error responses,
including `500`s, carry the baseline too. Applies on every response:

| Header | Value | Condition |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | always |
| `X-Frame-Options` | `DENY` | always |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | always |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains` | only when `IHostEnvironment.IsProduction()` is `true` |

HSTS is gated on the **hosting environment**
(`IHostEnvironment.IsProduction()`), **not** `HttpContext.Request.IsHttps`:
behind a TLS-terminating proxy — the expected production topology — the
inbound request to Kestrel is plain HTTP even though the client connection
was HTTPS, so gating on the request's scheme would silently make HSTS never
fire in production.

**Deliberately deferred, not decided:** `Content-Security-Policy` and
`frame-ancestors` — their correct value depends on the not-yet-built
frontend/deployment topology.

## CORS

Config-bound `Cors:AllowedOrigins` (`SquadCrm.Api.CorsOptions`, section
`Cors`). An absent or empty section blocks every cross-origin request. The
host **fails fast** at startup if any configured origin is the wildcard
`*` — an insecure policy is never silently configured.

## API versioning (D5)

`/api/v1` is the adopted route-prefix convention for new endpoints (see
`ArchitectureFixtureModule.ModuleInfoPageRoute`). This is a **route-prefix
and documentation convention only** — no version-negotiation framework, no
`Asp.Versioning`/`Microsoft.AspNetCore.Mvc.Versioning` package, no
content-negotiation or header-based versioning machinery is introduced.

## What's explicitly NOT here yet

| Not here | Owning story |
|---|---|
| Authentication scheme, session, login/logout endpoint | CRM-110 |
| A real identity model behind `ICurrentUserAccessor` | CRM-110 |
| Audit logging | CRM-114 |
| Domain/integration events, transactional outbox | CRM-198 |
| File storage adapters | CRM-200 |
| OpenTelemetry observability pipeline, `Activity`-based tracing | CRM-201 |
| Broader architecture-test suite build-out | CRM-202 |
| Content-Security-Policy / `frame-ancestors` headers | undecided — frontend/deployment topology dependent |
| Version-negotiation framework beyond the `/api/v1` route prefix | undecided |
