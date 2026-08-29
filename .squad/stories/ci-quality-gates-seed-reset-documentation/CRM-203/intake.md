# Story intake — CRM-203

## Feature

- **Feature:** CI Quality Gates, Seed/Reset & Technical Documentation
- **Tracker:** Linear `CRM-203`
- **Status:** In Progress
- **Milestone:** Sprint 0 — Project Setup
- **Priority / estimate:** High / 3 points

## Requirements

- CI builds backend and Angular applications and runs lint, format, automated, architecture and migration validation gates.
- Provide documented development/test seed and reset using synthetic data only.
- Document current architecture boundaries and a controlled emergency CI-bypass process.

## Dependencies and evidence

- CRM-107, CRM-201 and CRM-202 are Done and merged in `origin/main`.
- CRM-202's `.github/workflows/tests.yml` is the test execution foundation to extend, not replace.
- Existing EF migrations, module-owned ArchitectureFixture schema and local Compose PostgreSQL are the current reset/seed inputs.

## Scope boundary

- No production seeding, startup migrations, generic seed framework, deployment pipeline, business module/data, downstream feature or additional infrastructure.
- No redesign of CRM-202 tests; add only CRM-203's explicit comprehensive gates.
