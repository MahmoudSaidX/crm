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
