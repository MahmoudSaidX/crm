# CRM-117 — Responsive Web & Mobile-Friendly Application Shell

## Status

Ready for implementation. Linear is the product source of truth; CRM-104 and CRM-116 are Done and merged into `origin/main`.

## Story

As a CRM user, I want core workflows to work across desktop, tablet and mobile-sized screens so that the CRM remains usable from different devices.

## Acceptance criteria

- Shared Agent CRM and Customer Portal shells adapt navigation/content to desktop, tablet and mobile breakpoints.
- Forms, lists/tables, drawers and action areas remain usable without unintended horizontal page scrolling.
- Dense tables use an explicit responsive representation.
- Touch targets, keyboard focus and responsive overlays meet the baseline accessibility behavior.
- RTL and LTR remain usable at supported viewport sizes.
- Shared responsive primitives are reusable and have representative tests.

## Business rules

- Mobile-friendly means responsive web, not a native application.
- Feature pages use shared breakpoint/layout conventions.
- Viewport-based hiding is not authorization.
- Critical primary actions remain discoverable.
- Semantics and content order remain logical when layout changes.

## Scope and constraints

- Use Angular, PrimeNG and PrimeIcons per ADR-009.
- Preserve CRM-116 localization and global direction behavior per ADR-010.
- Introduce no persistence or business model.
- Do not implement CRM-120, CRM-141, CRM-172 or other downstream stories.
