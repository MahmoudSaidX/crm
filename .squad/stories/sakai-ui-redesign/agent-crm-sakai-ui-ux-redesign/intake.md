# Story intake

- Folder: `.squad/stories/sakai-ui-redesign/agent-crm-sakai-ui-ux-redesign/intake.md`

---

## Feature

- **Feature name (display):** Agent CRM Sakai UI/UX redesign
- **Feature slug (folder under `plans/`):** `sakai-ui-redesign`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-SAKAI-UI`
- **Work item type:** `Story`
- **Status:** `Ready`
- **Assignee:** mahmoud.saeed@azm.com.sa
- **Labels:** frontend, design-system, ui

---

## Title

```
Project-wide Agent CRM UI/UX redesign on the PrimeNG Sakai foundation
```

---

## Description

```
Replace the current inconsistent Agent CRM visual design with a cohesive, professional design based
on the official free/open-source PrimeNG Sakai template (https://github.com/primefaces/sakai-ng,
MIT licensed).

This is a VISUAL/LAYOUT migration, not a functional rewrite. The final Agent CRM must clearly look
and feel like a Sakai-based PrimeNG application while preserving all existing Squad CRM
functionality, routes, permissions, APIs, validation, localization and tests.

Sakai is integrated INTO the existing Angular workspace. The existing application is not replaced,
no new Angular workspace is created, no feature is rebuilt inside a copied Sakai project, and no
Sakai demo page/feature is imported. Flow: Sakai -> theme/layout/design patterns -> existing
shared-ui -> existing Agent CRM features.

Establish the shared foundation first (theme/tokens, typography, spacing, shell, sidebar, topbar,
navigation, responsive shell, common content layout, shared visual patterns), then migrate every
currently implemented Agent CRM screen to that one coherent visual system. The application must not
end up half old-design and half Sakai.

### Current state (verified by repository inspection)

Workspace: `src/frontend` — Angular 20.3, PrimeNG 20.4, `@primeng/themes` 20.4 (Aura preset),
primeicons 8, standalone components, signals, Karma/Jasmine, ESLint + Prettier.
Projects: `agent-crm`, `customer-portal`, `platform` (lib), `shared-ui` (lib).

Theme today: `shared-ui/lib/theme/provide-prime-ng-platform.ts` — stock Aura preset, `cssLayer`
`theme, base, primeng`, ripple on. No design tokens, no brand palette, no dark mode.
`agent-crm/src/styles.scss` is ~20 lines of reset only.

Shell today: `shared-ui/lib/layout/responsive-shell.ts|html|scss` — hand-written topbar + flat
`<a>` desktop sidebar + PrimeNG Drawer mobile nav, already using logical CSS properties and
`--p-*` tokens. `agent-crm/app/shell/agent-shell.ts` builds a FLAT, permission-gated
`ShellNavigationItem[]` (no grouping), plus branding title/logo, language switcher and sign-out.

Screens today (all currently implemented, all in scope): login, forbidden, home, roles (list, form,
permissions), departments (list, form), branches (list, form), staff users (list, form, roles),
system configuration (list, form), branding settings, audit (list, detail), customers (list, form,
detail), ticket categories (list, form), ticket priorities (list, form), ticket create, tickets
(list, detail), my tickets. Roughly 1,200 lines of ad-hoc per-page SCSS duplicating page-header,
filter-toolbar, form-grid and detail-grid patterns.

Localization: `platform/lib/i18n` owns locale + `<html lang|dir>`; per-feature `*-translations.ts`
files; EN/AR with RTL already supported and must not regress.

### Sakai reference (verified)

Sakai `master` is Angular 21 / PrimeNG 21 / `@primeuix/themes` 2 / primeicons 7 — NOT our versions.
Sakai tag **20.0.0** is Angular 20 / PrimeNG 20 / `@primeuix/themes` 1.2 and is the correct
reference for this story. Our versions stay authoritative; no Angular/PrimeNG upgrade or downgrade.

Relevant Sakai parts (small and portable): `src/app/layout/component/app.layout.ts`,
`app.topbar.ts`, `app.sidebar.ts`, `app.menu.ts`, `app.menuitem.ts`, `app.footer.ts`,
`app.configurator.ts`, `src/app/layout/service/layout.service.ts` (~1,200 lines total) and
`src/assets/layout/*.scss` + `variables/_light.scss|_dark.scss` (~690 lines total).
Not in scope to copy: `src/app/pages/**` demos, `src/assets/demo/**`, flags, chart.js, quill.

Sakai 20.0.0 layout templates use Tailwind CSS 4 utility classes plus `tailwindcss-primeui`, and its
layout SCSS uses ~44 physical `left`/`right` declarations that are not RTL-safe.
```

---

## Acceptance criteria

```
Foundation
- [ ] Sakai theme layer adopted in `shared-ui` (preset + design tokens + typography + spacing),
      compatible with PrimeNG 20.4 / `@primeng/themes`; no Angular/PrimeNG version change.
- [ ] Sakai application shell adopted: topbar, sidebar, hierarchical menu, content container,
      footer, responsive/overlay menu behaviour.
- [ ] Existing permission-gated navigation model preserved; navigation may be grouped into Sakai
      menu sections but every item stays gated by its current permission.
- [ ] Branding (product name, logo) and the language switcher remain in the shell.
- [ ] Light/dark mode adopted only if it is clean at PrimeNG 20; no extra theme configurability.
- [ ] Exactly one shell implementation remains; the superseded shell/styles are deleted.

Screens
- [ ] Every currently implemented Agent CRM screen listed in the Description uses the new system.
- [ ] List pages follow one pattern: page header -> search/filters/actions -> table -> pagination,
      with server-side filtering/sorting/pagination behaviour unchanged.
- [ ] Forms follow one pattern: labels, required indicators, help text, validation messages, field
      grouping, responsive columns, consistent submit/cancel placement. Validation rules unchanged.
- [ ] Detail pages have a real information hierarchy, not a random stack of cards.
- [ ] Ticket Detail is redesigned as a cohesive Sakai-style ticket workspace with clear separation
      of: primary ticket info, current state/ownership, context/details, actions, history.
      All permission gates, actions (assign/reassign, status transition, manual escalation) and
      backend behaviour preserved.
- [ ] Empty, loading and error states are consistent across screens.

Quality gates
- [ ] EN/LTR and AR/RTL verified for shell, sidebar, topbar, menus, breadcrumbs/page headers,
      forms, tables, pagination, dropdowns, dialogs, overlays, detail layouts, action areas and
      mobile navigation. Logical CSS properties used; Sakai's physical left/right rules ported.
- [ ] Responsive verified at ~1280px, ~768px, ~390px with no horizontal page overflow.
- [ ] `ng test` (full agent-crm suite), `ng lint`, `prettier --check`, production agent-crm build
      all pass. Existing tests preserved; test selectors (`data-testid`) preserved or updated
      together with their specs.
- [ ] Production bundle size recorded before and after; only actually-used Sakai/Tailwind code ships.
- [ ] No backend/domain/API change.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None. | Sakai reference is cloned from GitHub at tag `20.0.0`. |

---

## Dependencies

- **Blocked by / related ids:** none.
- **Depends on code areas or other stories:** `src/frontend/projects/shared-ui`,
  `src/frontend/projects/platform` (i18n/branding), `src/frontend/projects/agent-crm` (all features).
  ADR-009 (Angular + PrimeNG), ADR-010 (localization/RTL/responsive), ADR-011 (testing).

## Extra notes (optional)

- Open decisions for the plan to resolve explicitly:
  1. **Tailwind CSS 4.** Sakai 20 layout templates are written in Tailwind utilities +
     `tailwindcss-primeui`. Either adopt Tailwind for the frontend workspace (Sakai-idiomatic,
     new cross-cutting build dependency — likely needs a short ADR) or port the layout templates to
     plain SCSS using `--p-*` tokens (no new dependency, less faithful, more hand-written CSS).
     Tailwind is a utility CSS framework, not a competing UI component library, so it does not
     violate the ADR-009 "no second UI library" rule, but it is cross-cutting.
  2. **Theme package.** We use `@primeng/themes`; Sakai 20 uses `@primeuix/themes`. Confirm which
     one PrimeNG 20.4 resolves to before changing the import.
  3. **Sakai layout scope.** `app.configurator.ts` (446 lines of theme-picker UI) is optional
     product surface; default is to skip it and expose at most a dark-mode toggle.
- Sakai is MIT licensed (PrimeTek, 2018-2026); retain attribution where source files are adapted.

## Technical hints (optional)

- Repo root `.`; frontend workspace `src/frontend`; primary language `typescript`.
- Build/verify: `npm run build:agent-crm`, `npm test`, `npm run lint`, `npm run format:check`
  (run from `src/frontend`).
- Sakai reference checkout used during inspection:
  `git clone --depth 1 --branch 20.0.0 https://github.com/primefaces/sakai-ng.git`.
- `customer-portal` shares `shared-ui`; any change to `ResponsiveShell` or
  `providePrimeNgPlatform` must keep `customer-portal` building and its tests green.

## Out of scope

- Customer Portal redesign (it must keep building, but is not visually migrated in this story).
- New CRM functionality, business-rule changes, backend/domain/API changes.
- Sakai demo pages, demo assets, charts, editors and the theme configurator panel.
- Angular/PrimeNG version upgrades and unrelated refactoring or cleanup.
