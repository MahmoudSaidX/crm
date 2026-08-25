# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/repository-and-developer-workflow/repository-developer-workflow/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Repository & Developer Workflow
- **Feature slug (folder under `plans/`):** `repository-and-developer-workflow`

## Tracker (metadata only)

- **Tracker type:** `Linear`
- **Work item id:** `CRM-107`
- **Work item type:** `Story`
- **Status:** `Todo`
- **Assignee:** `Mahmoud Said`
- **Labels:** `foundation`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```text
Repository & Developer Workflow
```

---

## Description

```md
## User Story

As a developer, I want a reproducible repository and developer workflow so that contributors can build, test and run Squad CRM consistently.

## Business Rules

- Secrets and local credentials must never be committed.
- Developer setup must be reproducible and automation-first.
- Documentation changes are required when setup commands/configuration change.

## Fields Dictionary

No business data-entry fields in this story.
```

---

## Acceptance criteria

```md
- [ ] Repository structure reflects Angular applications, ASP.NET Core modular monolith and supporting infrastructure.
- [ ] README documents local setup, migrations, tests and common commands.
- [ ] Editor, lint/format and Git ignore rules are committed.
- [ ] Branch/commit conventions are documented.
- [ ] Sample environment configuration is provided without secrets.
- [ ] A fresh clone can be started using the documented workflow.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** None.
- **Depends on code areas or other stories:** None. This is the initial repository/developer-workflow foundation.

### Stories blocked by this story

- `CRM-104` — Angular Workspace — Agent CRM & Customer Portal
- `CRM-105` — ASP.NET Core Modular Monolith Foundation
- `CRM-197` — Docker Compose & Local Infrastructure
- `CRM-203` — CI Quality Gates, Seed/Reset & Technical Documentation

---

## Extra notes (optional)

- This is the first implementation story in Sprint 0.
- The repository must be prepared for the agreed architecture without prematurely implementing the feature stories that follow.
- The repository will contain two primary application areas:
  - Angular frontend.
  - ASP.NET Core backend modular monolith.
- PostgreSQL is the selected database for later persistence setup.
- PrimeNG is the primary Angular UI component library.
- PrimeIcons may be used where appropriate.
- Do not introduce Angular Material or another competing broad UI component library.
- External providers will later use provider-neutral ports/adapters.
- The repository already contains project-level documentation under `docs/` and Claude instructions in `CLAUDE.md`.
- Existing `.squad/` and `.claude/` structures belong to Squad Kit / Claude Code and must be preserved.

---

## Technical hints (optional)

- Repos/roots: `.`
- Primary frontend language: `typescript`
- Frontend framework: `Angular`
- Primary UI library: `PrimeNG`
- Backend: `ASP.NET Core`
- Backend architecture: `Modular Monolith`
- Database selected for subsequent setup: `PostgreSQL`
- ORM selected for subsequent setup: `EF Core`

### Expected high-level repository direction

The exact structure should be decided by the implementation plan while respecting the project ADRs, but the repository should be able to accommodate:

```text
/
├── .claude/
├── .squad/
├── docs/
├── src/
│   ├── frontend/
│   └── backend/
├── tests/
├── .editorconfig
├── .gitignore
├── CLAUDE.md
└── README.md
```

Do not treat this tree as a requirement to create empty/artificial folders. The implementation plan may refine it based on Angular and .NET workspace conventions.

### Architecture constraints

- Preserve modular-monolith boundaries.
- Do not create direct cross-module persistence dependencies.
- Do not commit secrets.
- Prefer reproducible scripts/commands over manual setup instructions.
- Keep operating-system-specific setup to a minimum.
- Commands documented in README must correspond to actual repository commands/scripts.
- Do not implement PostgreSQL persistence, Outbox, Hangfire, observability, file storage or CI in this story beyond what is strictly necessary for the repository baseline; those have dedicated Sprint 0 stories.
- Do not implement CRM business modules/features in this story.

---

## Out of scope

- Implementing Agent CRM screens.
- Implementing Customer Portal screens.
- Implementing CRM business modules.
- PostgreSQL/EF Core persistence implementation (`CRM-106`).
- Docker Compose/local infrastructure implementation (`CRM-197`).
- Transactional Outbox implementation (`CRM-198`).
- Hangfire/background processing implementation (`CRM-199`).
- File storage implementation (`CRM-200`).
- Observability implementation (`CRM-201`).
- Full automated testing foundation (`CRM-202`).
- CI quality gates and seed/reset implementation (`CRM-203`).
- Shared API/security foundation (`CRM-204`).
- Production deployment infrastructure.