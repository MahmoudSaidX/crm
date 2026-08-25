# Frontend --- Angular + PrimeNG

-   Angular + TypeScript.
-   PrimeNG is the primary component library; PrimeIcons where
    appropriate.
-   Check PrimeNG before creating a custom standard control.
-   Do not add Angular Material/another broad UI library without an ADR.
-   Do not wrap every PrimeNG component. Shared wrappers require
    repeated design/business/accessibility behavior.
-   Centralize PrimeNG theme/design tokens and shared configuration
    rather than scattering overrides.
-   Keep business logic outside presentation components.
-   Verify RTL, keyboard/accessibility and responsive behavior for
    customized components.
-   Tables require an intentional mobile/tablet strategy; do not merely
    squeeze desktop tables.
-   Agent CRM and Customer Portal share only genuinely reusable
    libraries.

The Angular workspace implementing these rules lives at `src/frontend/`. It
hosts the Agent CRM and Customer Portal applications plus the
`@squad-crm/platform` and `@squad-crm/shared-ui` libraries, whose dependency
boundaries are enforced by ESLint. The two libraries are siblings: neither may
depend on the other, and neither may depend on an application. See
[`src/frontend/README.md`](../../src/frontend/README.md) for the workspace
layout, the runtime configuration contract and the localization/direction
foundation.
