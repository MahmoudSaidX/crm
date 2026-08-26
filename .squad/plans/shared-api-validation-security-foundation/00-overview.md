# shared-api-validation-security-foundation — plan overview

Entry point for the **shared-api-validation-security-foundation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | `06-story-shared-api-validation-security-foundation.md` | Shared API, Validation & Security Foundation | CRM-204 | Story 03 (CRM-105), Story 05 (CRM-106) |

## Dependency notes

- **Depends on** the modular monolith foundation from CRM-105 (Story 03 — `IModule` composition, RFC 9457 Problem Details, `CorrelationIdMiddleware`, `ValidationEndpointFilter<T>`, environment-aware CORS) and, only for sequencing/no-conflict purposes, the persistence work from CRM-106 (Story 05 — not modified by this story).
- **Generalizes, does not replace**, CRM-105's error/correlation/validation seams: `SquadCrm.BuildingBlocks.Errors`, `SquadCrm.BuildingBlocks.Correlation`, `SquadCrm.BuildingBlocks.Validation` are extended with new `SquadCrm.BuildingBlocks.Http` (pagination) and `SquadCrm.BuildingBlocks.Security` (narrowed authorization extension point + security headers) folders in the same project — no new project.
- **Blocks** CRM-110 (auth/session — consumes the narrowed `ICurrentUserAccessor` seam, which fails DI resolution until CRM-110 registers an implementation, and `AddSquadCrmAuthorizationExtensionPoint()`), CRM-114 (audit — also inherits the documented cursor/keyset pagination extension point), CRM-198 (events/outbox), CRM-200 (file storage), CRM-201 (OpenTelemetry — must preserve the `correlationId` vs. `traceId` divergence contract), CRM-202 (test infrastructure). None of their business logic is implemented here.
- **Shared contract:** `ForbiddenAssemblyPrefixes` in `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` loses only `Microsoft.AspNetCore.Authorization.`; `Microsoft.AspNetCore.Authentication.` stays forbidden until CRM-110 adds a real scheme.
- **Open questions resolved by the approved amendment** (D1–D5, user-approved): single-resource success responses are bare DTOs (D4, no envelope); error bodies carry a module-owned `code` extension plus `correlationId` distinct from `traceId` (D1, D2); `ICurrentUserAccessor` is narrowed to `IsAuthenticated` + an opaque handle with no default registration (D3); `/api/v1` is adopted as a route-prefix convention only (D5). **Still open, unchanged:** no authentication scheme is chosen (CRM-110); the security-header baseline excludes CSP/`frame-ancestors`, deferred pending the frontend/deployment topology.
- Implements `docs/adr/ADR-003-api-errors.md` (amended by this story with a dated addendum recording the D4 success-response refinement — the one ADR edit this plan authorizes) and prepares for `docs/adr/ADR-004-auth-authorization.md`, which stays unamended. No new ADR required.
