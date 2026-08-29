# Story 12 — CI Quality Gates, Seed/Reset & Technical Documentation (CRM-203)

## Goal

Finish Sprint 0 with the explicit build/lint/format/migration CI gates, a repeatable synthetic development reset/seed workflow, and concise documentation of current module boundaries and merge policy.

## Implementation

1. Extend CRM-202's existing workflow in place with backend/frontend builds, formatting, frontend lint and pending-model-change validation; retain its existing tests and PostgreSQL service.
2. Add module-owned, synthetic ArchitectureFixture development seed SQL and small root scripts for migration, seeding and destructive reset/rebuild. Do not add a generic seeding framework or production startup behavior.
3. Document exact commands, destructive safeguards, required CI checks, the emergency bypass process and current architecture/module boundaries.
4. Add no business data, real CRM module, downstream feature, deployment pipeline or speculative CI abstraction.

## Verification

- Reproduce every backend and frontend workflow command locally.
- Reset a disposable local Compose volume, apply migrations and seed it; verify the synthetic row and rerun seed to prove idempotency.
- `git diff --check`; inspect workflow and scripts for secrets, destructive-target safety and CRM-203 scope.
