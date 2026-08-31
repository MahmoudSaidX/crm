# CRM-117 — Responsive Web & Mobile-Friendly Application Shell

## Reconciliation

CRM-104 and CRM-116 are Done and merged. No conflicting approved plan exists. ADR-009 requires Angular/PrimeNG; ADR-010 requires shared responsive conventions and global RTL/LTR. Current routes are flat and explicitly reserve the real shell for CRM-117. Existing roles pages are the only dense/list/form feature available for representative integration.

## Implementation plan

1. Add a shared-ui responsive shell using PrimeNG navigation/overlay controls, logical CSS properties, one shared breakpoint, accessible landmarks, visible focus and minimum touch targets.
2. Add thin localized Agent CRM and Customer Portal shell components and nest their existing routes beneath them; keep agent login outside the authenticated shell.
3. Make the existing role table explicitly horizontally scrollable and make existing action/form areas wrap without page overflow.
4. Add representative shell and route tests covering navigation, mobile overlay behavior, nested routing and locale-direction compatibility.
5. Run frontend formatting, lint, tests and production builds; then visually verify both apps at desktop/mobile widths in English and Arabic.

## Out of scope

Native mobile apps, branding/theming, new feature navigation, permissions, persistence, device detection as business state, and all stories downstream of CRM-117.
