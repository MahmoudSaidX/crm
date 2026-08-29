# Story 11 — Automated Testing & Architecture Tests (CRM-202)

## Goal

Complete the deterministic backend unit/integration, frontend unit/component, architecture-boundary and isolated PostgreSQL test foundation, then run representative suites locally and through minimal test-only CI.

## Approved scope ruling

CRM-202 owns deterministic test commands and the minimum CI workflow/setup needed to execute its representative automated and architecture suites. CRM-203 retains full build, frontend lint/build, repository-wide formatting, migration validation beyond these tests, seed/reset tooling, broader quality-gate orchestration, branch/merge enforcement and CI hardening. The CRM-202 workflow must remain a small extension point for CRM-203, not a speculative CI framework.

## Implementation

1. Add a backend unit-test project with representative deterministic business-foundation tests and include it in the solution.
2. Preserve and exercise the existing API/integration and Angular unit/component test baselines.
3. Extend architecture tests with generic project-reference rules that prohibit module implementation and persistence access across module boundaries.
4. Make PostgreSQL integration tests create, migrate and remove an isolated per-run test database automatically; never reset the configured development database.
5. Add one minimal GitHub Actions workflow that runs backend unit/API/architecture/PostgreSQL integration suites and frontend tests only.
6. Document exact local commands, test isolation and the CRM-202/CRM-203 boundary.

## Verification

- `dotnet test src/backend/SquadCrm.sln --no-restore`
- `npm test --prefix src/frontend -- --no-progress`
- Validate the test-only workflow syntax and reproduce every workflow command locally against PostgreSQL.
- `dotnet format src/backend/SquadCrm.sln --no-restore --verify-no-changes`
- `npm run format:check --prefix src/frontend`
- `git diff --check` and final acceptance/security/architecture/scope review.
