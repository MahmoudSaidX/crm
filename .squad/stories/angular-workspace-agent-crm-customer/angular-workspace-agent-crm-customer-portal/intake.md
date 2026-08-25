# Story intake

Fill this template for each story you want planned. Keep it
copy-paste-friendly: the planner reads **this file and the files in
`attachments/`**, nothing else.

-   Folder:
    `.squad/stories/angular-workspace-agent-crm-customer-portal/angular-workspace-agent-crm-customer-portal/intake.md`
-   Binaries (screenshots, PDFs, exports): put them in `attachments/`
    next to this file and list them below.
-   Do **not** rely on external links (tracker URLs, wiki, chat) --- the
    planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the
plan-generation meta-prompt bundled with squad-kit (`generate-plan.md`
in the installed package).

------------------------------------------------------------------------

## Feature

-   **Feature name (display):** Angular Workspace --- Agent CRM &
    Customer Portal
-   **Feature slug (folder under `plans/`):**
    `angular-workspace-agent-crm-customer-portal`

## Tracker (metadata only)

-   **Tracker type:** `Linear`
-   **Work item id:** `CRM-104`
-   **Work item type:** `Story`
-   **Status:** `Todo`
-   **Assignee:** `Mahmoud Said`
-   **Labels:** `foundation`

External tracker links are **not** followed by the planner. Keep the id
for naming and traceability only.

------------------------------------------------------------------------

## Title

``` text
[Sprint 0] Angular Workspace — Agent CRM & Customer Portal
```

------------------------------------------------------------------------

## Description

``` md
## User Story

As a developer, I want the Angular frontend workspace structured for the Agent CRM and Customer Portal so that both applications can evolve consistently on shared platform foundations.

## Business Rules

- Frontend is Angular.
- Agent CRM and Customer Portal are separate application surfaces but share approved reusable libraries.
- Business modules must not depend on each other's internal implementation details.
- Environment-specific values must not be hard-coded.

## Fields Dictionary

No business data-entry fields in this story. Environment/configuration keys are implementation configuration, not CRM domain fields.
```

------------------------------------------------------------------------

## Acceptance criteria

``` md
- [ ] Angular workspace contains separate Agent CRM and Customer Portal applications with reusable shared libraries.
- [ ] Routing, environment configuration, HTTP client, API base URL, global styles, linting and formatting are configured.
- [ ] Shared UI/core infrastructure supports Arabic/English and RTL/LTR from the start.
- [ ] Production builds for both applications succeed.
- [ ] Feature code is organized by business capability rather than a single global feature folder.
```

------------------------------------------------------------------------

## Attachments

None.

------------------------------------------------------------------------

## Dependencies

-   **Blocked by / related ids:** `CRM-107` --- Repository & Developer
    Workflow --- completed.
-   **Depends on code areas or other stories:** Repository baseline
    created by CRM-107.

### Stories blocked by this story

-   `CRM-204` --- Shared API, Validation & Security Foundation
-   `CRM-202` --- Automated Testing & Architecture Tests
-   `CRM-117` --- Responsive Web & Mobile-Friendly Application Shell
-   `CRM-116` --- Arabic & English Localization
-   `CRM-110` --- User Authentication & Session Management

------------------------------------------------------------------------

## Extra notes (optional)

-   This story establishes the frontend workspace and platform
    foundation; it does not implement CRM business screens/features.
-   **PrimeNG is the primary UI component library.**
-   Use **PrimeIcons** where appropriate.
-   Do not add Angular Material or another competing broad UI component
    library unless a future ADR explicitly approves it.
-   Prefer PrimeNG for standard UI primitives when suitable instead of
    hand-building equivalent components.
-   Do not create wrappers around every PrimeNG component. Shared
    wrappers/components are justified only for repeated Squad CRM
    design, business or accessibility behavior.
-   PrimeNG is the implementation component library; it does not replace
    approved Squad CRM UX/design decisions.
-   Arabic/English and RTL/LTR capability must be present at foundation
    level, but full localization feature work belongs to `CRM-116`.
-   Responsive foundations should not attempt to complete the dedicated
    responsive application-shell story `CRM-117`.
-   Agent CRM and Customer Portal are distinct application surfaces and
    must not leak application-specific business implementation into
    shared libraries.
-   Existing `.claude/`, `.squad/`, `docs/`, `CLAUDE.md`, repository
    workflow files and CRM-107 output must be preserved.

------------------------------------------------------------------------

## Technical hints (optional)

-   Repo/root: `.`
-   Frontend area expected under the repository structure established by
    CRM-107.
-   Language: `TypeScript`
-   Framework: `Angular`
-   UI library: `PrimeNG`
-   Icons: `PrimeIcons`
-   Architecture reference: `docs/adr/ADR-009-angular-primeng.md`
-   Localization/responsive reference:
    `docs/adr/ADR-010-localization-responsive.md`
-   Project frontend architecture reference:
    `docs/architecture/frontend.md`

### Workspace direction

The implementation plan should choose Angular workspace/project/library
structure using current Angular conventions while preserving these
boundaries:

``` text
Frontend workspace
├── Agent CRM application
├── Customer Portal application
└── reusable shared libraries
    ├── core/platform concerns
    ├── shared UI/design-system concerns
    └── reusable cross-application utilities/contracts
```

Do not treat those names as mandatory folder names if Angular
tooling/current repository conventions suggest clearer names. Preserve
the architectural boundaries rather than blindly creating empty folders.

### PrimeNG baseline

The plan should include the minimum PrimeNG foundation required to prove
the library is correctly integrated into both applications, including
shared configuration/theming where appropriate.

Do not build a large custom design-system layer in this story. Establish
the integration point so later design-system/application-shell stories
can build on it.

### Environment/configuration

-   API base URL and other environment-specific values must come from
    environment/runtime configuration rather than being
    duplicated/hard-coded across applications.
-   Keep Agent CRM and Customer Portal configuration independently
    deployable where necessary while sharing the configuration
    mechanism.
-   Do not introduce secrets into frontend environment files.

### Routing and HTTP

-   Each application owns its top-level routes.
-   Establish shared HTTP/API infrastructure only for truly
    cross-application concerns.
-   Do not implement authentication/session behavior in this story;
    `CRM-110` owns that behavior.
-   Do not invent business API clients before their capabilities exist.

### Localization foundation

-   Establish the shared mechanism/conventions required for `ar` and
    `en`.
-   Direction must be derivable from locale (`ar` → RTL, `en` → LTR).
-   Do not attempt to translate future feature content in this story.

### Dependency boundaries

-   Shared libraries must not depend on Agent CRM or Customer Portal
    application code.
-   Agent CRM-specific business capabilities must not be imported by
    Customer Portal and vice versa.
-   Avoid a single global `features/` dumping ground; future feature
    code should be organized by business capability.

### Verification expected from the plan

At minimum, the generated plan should provide commands/checks proving:

-   dependency installation succeeds;
-   Agent CRM development/build configuration is valid;
-   Customer Portal development/build configuration is valid;
-   production build succeeds for both applications;
-   lint/format checks succeed;
-   PrimeNG integration compiles in both surfaces;
-   Arabic/English direction switching foundation can be verified
    without implementing CRM-116 in full;
-   no Angular Material/competing broad UI library was introduced.

------------------------------------------------------------------------

## Out of scope

-   CRM business feature screens/components.
-   Full Agent CRM application shell UX (`CRM-117` owns responsive shell
    behavior).
-   Full Customer Portal feature implementation.
-   Full localization content/translation implementation (`CRM-116`).
-   Authentication/session management (`CRM-110`).
-   Backend/API implementation (`CRM-105`, `CRM-204`).
-   PostgreSQL/EF Core implementation (`CRM-106`).
-   Docker Compose/local infrastructure (`CRM-197`).
-   Full automated testing/architecture-test foundation (`CRM-202`),
    beyond minimal tests/checks required to verify this story safely.
-   CI quality gates (`CRM-203`).
-   Building a complete custom design system on top of PrimeNG.
-   Adding Angular Material or another competing broad UI component
    library.