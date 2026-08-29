# Story intake — CRM-202

## Feature

- **Feature:** Automated Testing & Architecture Tests
- **Tracker:** Linear `CRM-202`
- **Status:** In Progress
- **Milestone:** Sprint 0 — Project Setup
- **Priority / estimate:** Urgent / 5 points

## Requirements

- Configure backend unit/integration and frontend unit/component test foundations.
- Enforce modular-monolith and cross-module persistence boundaries with executable architecture rules.
- Automate isolated test-database setup/reset.
- Run representative smoke tests deterministically locally and in minimal test-only CI.

## Dependencies and evidence

- CRM-104, CRM-105, CRM-106, CRM-197, CRM-198, CRM-199, CRM-200, CRM-201 and CRM-204 are Done and merged in `origin/main`.
- Existing API, persistence integration, architecture and Angular tests are current implementation inputs, not work to recreate.
- ADR-011 requires backend unit/integration, frontend unit/component and executable architecture tests reproducible in CI.

## Scope boundary

- Approved ruling: CRM-202 may add only the minimal test CI required by its own acceptance criterion.
- CRM-203 retains comprehensive quality gates, builds/lint/format enforcement, broader migration validation, seed/reset tooling, policy/enforcement and CI hardening.
- No business modules, production persistence behavior, seed data, production services or downstream-story behavior.
