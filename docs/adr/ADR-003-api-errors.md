# ADR-003 --- API & Error Conventions

**Status:** Accepted baseline.

## Decision

Use explicit DTOs, consistent validation/errors, bounded pagination,
correlation IDs and safe exception mapping. Version external APIs.

## Rule

Story plans may refine details but must not silently contradict this
ADR.

## Addendum (2026-08-26 — CRM-204, D4)

Refines the success/error/pagination shape without contradicting the
Decision above:

- Single-resource and normal success responses are **bare DTOs** — no
  wrapper envelope.
- Paginated success responses use `PagedResult<T>`
  (`SquadCrm.BuildingBlocks.Http`): `Items`, `Page`, `PageSize`,
  `TotalCount`.
- Errors use RFC 9457 Problem Details, now carrying `traceId`,
  `correlationId` and `code` extension members
  (`SquadCrm.BuildingBlocks.Errors.ProblemDetailsExtensions`). `code` is a
  stable, machine-readable value each module declares and owns under its
  own naming convention — this ADR does not introduce a central registry
  of every module's codes. `correlationId` (the stable client/support
  handle from `CorrelationIdMiddleware`) and `traceId` (the
  observability/tracing identifier) are distinct and are never guaranteed
  equal.
- Cross-cutting response metadata (anything not part of the resource
  itself) travels in response headers unless a future story explicitly
  designs a different mechanism.

See `docs/api-conventions.md` for the full reusable reference.
`docs/adr/ADR-004-auth-authorization.md` is unaffected by this addendum.
