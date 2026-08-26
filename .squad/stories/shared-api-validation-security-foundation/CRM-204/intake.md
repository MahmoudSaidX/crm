# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/shared-api-validation-security-foundation/CRM-204/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Shared API, Validation & Security Foundation
- **Feature slug (folder under `plans/`):** `shared-api-validation-security-foundation`

## Tracker (metadata only)

- **Tracker type:** `Linear`
- **Work item id:** `CRM-204`
- **Work item type:** `Story`
- **Status:** `Todo` (reconciled NOT_STARTED: no intake, no plan, no code, no branch)
- **Assignee:** `Mahmoud Said`
- **Labels:** `foundation`, `security`
- **Milestone:** `Sprint 0 — Project Setup`
- **Priority:** `Urgent (1)`
- **Estimate:** `5 Points`
- **Source URL (metadata only, not followed by the planner):** https://linear.app/mahmoud-said/issue/CRM-204/sprint-0-shared-api-validation-and-security-foundation

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```
[Sprint 0] Shared API, Validation & Security Foundation
```

---

## Description

```md
## User Story

As a developer, I want common API, validation and security conventions so that all CRM modules expose predictable and secure application boundaries.

## Business Rules

- Validation errors must be field-addressable where applicable.
- Internal exception details and stack traces are never exposed to clients outside development.
- Authorization is enforced server-side; frontend visibility is not a security boundary.
- Secrets are configuration references, never API payloads or source-controlled values.

## Fields Dictionary

| Field | Meaning |
| --- | --- |
| Standard Error — Code | Stable machine-readable error code. |
| Standard Error — Message | Localized/display-safe message. |
| Standard Error — FieldErrors | Optional field-to-errors map. |
| Standard Error — CorrelationId | Trace identifier. |
| Pagination — Page | Page number, >= 1. |
| Pagination — PageSize | Bounded positive integer. |
| Pagination — TotalCount | Non-negative integer. |
```

---

## Acceptance criteria

```md
- [ ] Standard success/pagination/error conventions are documented and reusable.
- [ ] Request validation is centralized at application boundaries.
- [ ] Authentication/authorization extension points are prepared for Sprint 1.
- [ ] Correlation IDs and safe exception mapping are applied consistently.
- [ ] Security headers/CORS/configuration follow environment-aware defaults.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:**
  - `CRM-104` — Angular Workspace (Agent CRM & Customer Portal) — Done.
  - `CRM-105` — ASP.NET Core Modular Monolith Foundation — Done.
  - `CRM-106` — PostgreSQL + EF Core + Schema-per-Module — Done (referenced for context; not a listed blocker, already delivered).
- **Stories blocked by this story (from Linear, all Backlog):**
  - `CRM-198` — Domain/Integration Events & Outbox
  - `CRM-202` — Automated Testing & Architecture Tests
  - `CRM-201` — OpenTelemetry/Logging/Health
  - `CRM-200` — File Storage Foundation
  - `CRM-114` — Audit Actions
  - `CRM-110` — Auth & Session Management
- **Depends on code areas or other stories:**
  - `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Errors/` — `GlobalExceptionHandler.cs`, `ProblemDetailsExtensions.cs` (CRM-105): RFC 9457 Problem Details, safe `traceId`-only exception mapping, already wired via `AddSquadCrmProblemDetails()` / `AddExceptionHandler<GlobalExceptionHandler>()` in `Program.cs`.
  - `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Correlation/CorrelationIdMiddleware.cs` (CRM-105): reads/sanitises inbound `X-Correlation-Id`, promotes it to `HttpContext.TraceIdentifier`, echoes it on the response. Already registered in `Program.cs` via `app.UseMiddleware<CorrelationIdMiddleware>()`.
  - `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Validation/ValidationEndpointFilter.cs` (+ extensions) (CRM-105): DataAnnotations-based endpoint filter producing `HttpValidationProblemDetails` (field-addressable `errors` dictionary), explicitly documented in-code as "CRM-105 establishes the shape of validation only" and defers the long-term business-validation strategy to "the first stories that introduce real requests and endpoints".
  - `src/backend/src/Api/SquadCrm.Api/Program.cs`: composition root. Already wires Problem Details, exception handler, `CorsOptions` (config-bound allow-list under `Cors:AllowedOrigins`, fails fast on a `*` wildcard, no credentials), OpenAPI (Development-only), health checks (`/health`, liveness only), and `IModule` registration (`SquadCrm.BuildingBlocks.Modules`).
  - `src/backend/src/Api/SquadCrm.Api/appsettings.json` / `appsettings.Development.json`: existing `Cors:AllowedOrigins` binding (`[]` in base, `http://localhost:4200` in Development).
  - `env/backend.env.example`: existing `CORS__AllowedOrigins__0=...` environment contract (CRM-107/CRM-197 conventions) — any new environment-driven security configuration (headers, future auth) must follow this same array-binding convention, not introduce a competing one.
  - `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Modules/` (CRM-105 `IModule` abstraction) — any authentication/authorization extension point must compose through this existing module registration path, not bypass it.
  - `src/backend/tests/SquadCrm.ArchitectureTests/`, `src/backend/tests/SquadCrm.Api.Tests/` (CRM-105/CRM-106): existing architecture/API test projects that any new conventions should extend, per ADR-011 — CRM-202 owns build-out of the full architecture-test *suite*, but this story's own new conventions still need proving tests within the current scope, consistent with how CRM-105/CRM-106 added targeted tests for their own deliverables.

---

## Extra notes (optional)

- CRM-105 already delivered a working slice of this story's scope (RFC 9457 error contract, correlation id middleware, a DataAnnotations validation endpoint filter, environment-aware CORS with fail-fast wildcard rejection). CRM-105's own code comments explicitly flag validation strategy and (by omission) auth/authz and broader conventions as deferred to later stories — this story, CRM-204, is that follow-up for the *shared foundation* layer (not for any specific business endpoint).
- This is a **foundation** story. It documents/generalizes conventions and prepares **extension points** — it must not implement the business logic of the stories it blocks.

### Explicit non-goals (do not implement here)

- No actual authentication or session implementation (identity provider, token issuance/validation, login/logout, session storage/revocation) — that is `CRM-110`.
- No audit logging / audit trail implementation — that is `CRM-114`.
- No domain/integration events or transactional outbox — that is `CRM-198`.
- No OpenTelemetry wiring, structured logging pipeline, or health-check depth beyond what CRM-105 already ships — that is `CRM-201`.
- No file storage implementation — that is `CRM-200`.
- No build-out of the full architecture-test *suite* — that is `CRM-202` (this story may still add narrowly-scoped tests proving its own new conventions, consistent with CRM-105/CRM-106 precedent).
- Do not invent concrete business endpoints, DTOs, or entities merely to prove the conventions; reuse or minimally extend the existing `ArchitectureFixture` module as a non-business proving ground if a concrete endpoint is genuinely required to demonstrate an extension point end-to-end, following the same "must remain explicitly non-business and removable" rule CRM-106 established for it.

---

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `.` Primary language: `csharp` (backend, `net10.0`), with frontend impact only if the plan finds a genuinely shared contract concern (unlikely for this story — Acceptance Criteria are backend/API-boundary scoped).
- Backend: `src/backend/`, composition root `src/backend/src/Api/SquadCrm.Api/Program.cs`.
- Reuse and generalize, do not replace: `SquadCrm.BuildingBlocks.Errors` (Problem Details + `GlobalExceptionHandler`), `SquadCrm.BuildingBlocks.Correlation` (`CorrelationIdMiddleware`), `SquadCrm.BuildingBlocks.Validation` (`ValidationEndpointFilter<T>`).
- "Standard success/pagination conventions... documented and reusable" is new scope: no success-envelope or pagination-result type exists yet in the repo. The plan must decide the concrete shape from the intake's Fields Dictionary (`Page`, `PageSize`, `TotalCount`) without inventing unrequested fields (e.g. no `TotalPages`/`HasNext` unless the plan justifies it as a mechanical derivation of the given fields, not a new requirement).
- "Authentication/authorization extension points... prepared for Sprint 1" is new scope: no authentication/authorization middleware, policy scaffolding, or `IModule`-composable extension point exists yet. This must be an inert extension point (e.g. where/how CRM-110 will plug in), not a functioning auth mechanism.
- Security headers are new scope: no security-headers middleware exists yet (only CORS). "Environment-aware defaults" must follow the existing `builder.Environment.IsDevelopment()` pattern already used for OpenAPI/CORS-adjacent decisions in `Program.cs`, and any new configuration must follow the existing `env/backend.env.example` array/section-binding convention.
- Correlation IDs and safe exception mapping: largely already implemented (see Dependencies). Verify/extend consistency (e.g. correlation id present on validation-problem and health responses too) rather than re-implementing.
- EF Core / PostgreSQL / schema-per-module (CRM-106) is out of scope for this story except as prior art for "how a BuildingBlocks-level convention is documented and consumed by modules" — do not touch persistence code.

## Out of scope

- What this story explicitly does **not** cover:
  - Real authentication/session implementation (`CRM-110`).
  - Audit logging (`CRM-114`).
  - Domain/integration events and the transactional outbox (`CRM-198`).
  - OpenTelemetry/observability pipeline and deeper health checks (`CRM-201`).
  - File storage (`CRM-200`).
  - The full architecture-test suite build-out (`CRM-202`).
  - Any new business modules, entities, DTOs or endpoints beyond what is strictly necessary to prove the shared conventions.
  - PostgreSQL/EF Core/persistence changes (already owned by `CRM-106`).

## Open Questions

- The intake's Fields Dictionary defines `Standard Error` and `Pagination` shapes but the Linear item does not specify a **standard success envelope** shape for non-paginated responses (e.g. whether single-resource responses are wrapped at all, or returned as bare DTOs with only errors/pagination wrapped). The plan must not invent this without flagging it; if no existing repository convention resolves it, this is a decision for the user/product owner before implementation.
- Neither the Linear item nor any ADR specifies which authentication scheme CRM-110 will use (cookie session vs. bearer/JWT vs. both, for staff vs. customer portal identities per ADR-004's "distinct identity semantics"). The extension point requested here must be scheme-agnostic; the plan should not guess a scheme, only prepare a composition seam.
- The Linear item does not enumerate which specific security headers are required (e.g. `X-Content-Type-Options`, `X-Frame-Options`/`frame-ancestors`, `Referrer-Policy`, `Strict-Transport-Security`, `Content-Security-Policy`). No ADR enumerates a header set either. The plan should propose a conventional, low-risk baseline set and flag any header whose correct value depends on a not-yet-built frontend/deployment topology as an open item rather than guessing.
