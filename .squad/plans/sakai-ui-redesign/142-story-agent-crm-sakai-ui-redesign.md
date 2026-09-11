# Story 142 — Agent CRM Sakai UI/UX redesign

Branch: `feat/sakai-ui-redesign` (cut from `main` after CRM-141 merges).

---

## Prerequisites

- Story 16 completed: [`../responsive-application-shell/00-overview.md`](../responsive-application-shell/00-overview.md) — it created `projects/shared-ui/src/lib/layout/responsive-shell.*`, the shell this story supersedes.
- Story 15 completed: [`../arabic-english-localization/00-overview.md`](../arabic-english-localization/00-overview.md) — `LocalizationService` / `LocaleService` own `<html lang|dir>`; RTL must not regress.
- Story 141 completed: [`../agent-assigned-tickets-dashboard/141-story-crm-141-agent-assigned-tickets-dashboard.md`](../agent-assigned-tickets-dashboard/141-story-crm-141-agent-assigned-tickets-dashboard.md) — `my-tickets` is the newest screen and is in scope.
- ADR-009 (Angular + PrimeNG, no competing UI library), ADR-010 (localization/RTL/responsive), ADR-011 (testing) stay binding. **This story adds no ADR** — see *Design decision — no Tailwind*.
- Sakai reference checkout (read-only, outside the repo):
  `git clone --depth 1 --branch 20.0.0 https://github.com/primefaces/sakai-ng.git` — MIT, PrimeTek. Tag `20.0.0` is Angular 20 / PrimeNG 20, matching us. `master` is Angular 21 / PrimeNG 21 and **must not** be used.

---

## Story Goal

Replace the current ad-hoc Agent CRM visual design with one coherent Sakai-based design system, applied to **every** currently implemented Agent CRM screen.

User-visible outcomes:

1. A Sakai application shell: fixed topbar with logo/brand, menu toggle, topbar actions (language switcher, dark-mode toggle, sign-out); a static/overlay `layout-sidebar` with a grouped, collapsible, permission-gated menu; a `layout-main-container` content area with breadcrumb-style page header and footer.
2. One list-page pattern (page header → filter toolbar → table → paginator) and one form pattern across all screens.
3. Detail pages with real hierarchy; Ticket Detail rebuilt as a two-column ticket workspace instead of seven stacked `<section>`s.
4. Consistent empty / loading / error states.
5. Light and dark mode, EN/LTR and AR/RTL, desktop/tablet/mobile — all without regressions.

**Not in scope:** Customer Portal redesign (must keep building), new CRM functionality, business-rule or backend/API changes, Sakai demo pages/assets/charts/editor, the Sakai theme configurator panel, Angular/PrimeNG version changes.

---

## Context — Read These Files First

1. `src/frontend/package.json` — Angular **20.3**, PrimeNG **20.4**, `@primeng/themes` **20.4**, primeicons **8**. These versions are authoritative; do not change them.
2. `src/frontend/projects/shared-ui/src/lib/theme/provide-prime-ng-platform.ts` (whole file, 31 lines) — stock `Aura` preset, `cssLayer: { name: 'primeng', order: 'theme, base, primeng' }`, `ripple: true`. This is where the Sakai preset/tokens go. Note the existing comment stating brand tokens belong to "later design work" — that is this story.
3. `src/frontend/projects/shared-ui/src/lib/layout/responsive-shell.ts|.html|.scss` — the shell being replaced: `ShellNavigationItem { label, icon, routerLink, exact? }`, hand-written topbar, flat `<a>` sidebar, `p-drawer` mobile nav, `.sc-mobile-navigation` style class.
4. `src/frontend/projects/shared-ui/src/lib/layout/responsive-shell.spec.ts` — lines 25–49 query `main[tabindex="-1"]`, `[data-testid="mobile-menu-trigger"] button` and `.sc-mobile-navigation`. These selectors must keep working or the spec must be rewritten with the component.
5. `src/frontend/projects/agent-crm/src/app/shell/agent-shell.ts` — `navigationItems` computed, a **flat** list of 13 permission-gated entries (`roles.view`, `departments.view`, `ticketcategories.view`, `ticketpriorities.view`, `tickets.view` ×2, `tickets.create`, `branches.view`, `customers.manage`, `configuration.view`, `branding.view`, `users.view`, `audit.view`), plus `shellTitle` (branding-aware) and `signOut()`. Every gate must survive regrouping.
6. `src/frontend/projects/agent-crm/src/app/shell/agent-shell.html` — projects `<div shell-actions>` with `crm-agent-language-switcher` and the sign-out `p-button`.
7. `src/frontend/projects/agent-crm/src/styles.scss` (20 lines) — reset only; becomes the Sakai global entry.
8. `src/frontend/projects/agent-crm/src/app/customers/customer-list.html` lines 1–57 and `customer-list.scss` — the canonical current list page: `<main class="customers-page">`, `<header class="customers-header">` with `<h1>` + `crm-agent-language-switcher` + action button, `.customers-filters` div, then `p-table` with `[lazy]`, `[paginator]`, `[paginatorLocale]`, `(onLazyLoad)`. **Preserve every `p-table` binding.**
9. `src/frontend/projects/agent-crm/src/app/tickets/ticket-detail.html` (368 lines) — seven `<section class="ticket-detail-section">` blocks at lines 10, 88, 96, 126, 202, 303, 342 (identity+status action, customer, classification, routing+assignment, escalation, history, timestamps). This is the highest-value redesign target.
10. `src/frontend/projects/agent-crm/src/app/customers/customer-detail.html` (468 lines) — second largest detail screen (contacts, notes, attachments, timeline).
11. `src/frontend/projects/agent-crm/src/app/auth/login.html` + `login.scss` (75 lines) — standalone page outside the shell; gets the Sakai login layout.
12. `src/frontend/projects/platform/src/lib/i18n/locale.service.ts` lines ~10–52 — `direction` computed and `root.setAttribute('dir', …)`. RTL is document-global; the new shell must not hardcode physical sides.
13. `src/frontend/angular.json` — `agent-crm` build `styles` = `node_modules/primeicons/primeicons.css` + `projects/agent-crm/src/styles.scss`; budgets `initial` warn **500 kB** / error **1 MB**, `anyComponentStyle` warn **4 kB** / error **8 kB`.
14. Sakai reference files to port (all small):
    - `src/app/layout/component/app.layout.ts` (111 lines) — `containerClass` map (`layout-static`, `layout-static-inactive`, `layout-overlay`, `layout-overlay-active`, `layout-mobile-active`), outside-click handling, `blocked-scroll`.
    - `app.topbar.ts` (92), `app.sidebar.ts` (14), `app.menu.ts` (157), `app.menuitem.ts` (170), `app.footer.ts` (11).
    - `src/app/layout/service/layout.service.ts` (178) — `layoutConfig` / `layoutState` signals, `onMenuToggle()`, `isDarkTheme`, `.app-dark` toggling.
    - `src/assets/layout/*.scss` + `variables/_common.scss|_light.scss|_dark.scss` (~690 lines total) — plain SCSS mapping `--p-*` tokens onto `--surface-*` / `--primary-*` aliases. **No `@apply` anywhere** (verified by grep).
    - **Do not** port `app.configurator.ts` (446 lines), `src/app/pages/**`, `src/assets/demo/**`, `flags.scss`, chart.js or quill.

Grep aids:
- `` grep -rl 'crm-agent-language-switcher' src/frontend/projects/agent-crm/src --include='*.html' `` — **25 files** (24 pages + the shell). The per-page switcher is removed; only the topbar keeps one.
- `` grep -rhoE 'class="[a-z-]+-page"' src/frontend/projects/agent-crm/src --include='*.html' `` — **26** distinct page wrappers, each with its own near-duplicate SCSS (~1,200 lines total).

---

## Design decision — no Tailwind, port Sakai's SCSS

Sakai 20 ships Tailwind 4 + `tailwindcss-primeui`. We do **not** add them:

- Sakai's layout SCSS contains **zero** `@apply`/`theme()` calls — it is plain SCSS over `--p-*` custom properties and ports directly.
- Tailwind classes appear only ~8 times in Sakai layout templates (`flex`, `gap-2`, `items-center`, `hidden lg:block`, `relative`, `animate-fadein`), all in markup we rewrite anyway (our topbar carries branding, language switcher, sign-out — not Sakai's Calendar/Messages/Profile demo buttons).
- The intake's "only add a dependency when actually required" rule therefore bars it, and skipping it avoids a cross-cutting build change, an ADR, and unknown bundle cost on a build already **77.71 kB over** its initial-size warning budget.

`@primeng/themes` stays; **do not** switch to `@primeuix/themes`.

---

## Frontend Tasks

### 1 — Sakai theme layer in `shared-ui`

**File: `src/frontend/projects/shared-ui/src/lib/theme/provide-prime-ng-platform.ts`**

Keep `Aura` as the base preset and `cssLayer` order unchanged. Extend it with `definePreset` from `@primeng/themes` to apply Sakai's semantic choices (primary palette, surface palette, `colorScheme.light` / `colorScheme.dark` surface + text tokens), and set `options.darkModeSelector: '.app-dark'` so dark mode follows Sakai's class toggle.

```ts
const SakaiPreset = definePreset(Aura, {
  semantic: {
    primary: { /* Sakai default primary ramp */ },
    colorScheme: { light: { surface: { … } }, dark: { surface: { … } } },
  },
});
```

Keep the existing `provideAnimationsAsync()` and `ripple: true`. Replace the stale "brand tokens belong to later design work" comment with a pointer to this story.

**Create file: `src/frontend/projects/shared-ui/src/lib/layout/layout.service.ts`**

Port `LayoutService` from Sakai `layout.service.ts`, converted to repo style (signals, `providedIn: 'root'`, `readonly`, no `any`):

- `layoutConfig = signal<LayoutConfig>({ darkTheme: false, menuMode: 'static' })` — drop `preset`/`primary`/`surface` (configurator-only).
- `layoutState = signal<LayoutState>({ staticMenuDesktopInactive, overlayMenuActive, staticMenuMobileActive, menuHoverActive, … })`.
- `onMenuToggle()`, `isSidebarActive`, `isDarkTheme`, `isOverlay`, `menuSource$`/`resetSource$` for `AppMenuitem`.
- Toggle `.app-dark` on `document.documentElement` in an `effect`; persist the choice in `localStorage` under a `squad-crm.theme` key, matching how `LocaleService` persists locale.

### 2 — Sakai shell components in `shared-ui`

**Create files under `src/frontend/projects/shared-ui/src/lib/layout/`:**

- `app-layout.ts|.html` — port of Sakai `app.layout.ts`: `.layout-wrapper` + `containerClass`, `<sc-topbar>`, `<sc-sidebar>`, `.layout-main-container > .layout-main > <router-outlet>`, `<sc-footer>`, `.layout-mask`. Keep the outside-click listener, `NavigationEnd` auto-close and `blocked-scroll` body lock. Keep `<main tabindex="-1">` around the outlet so `responsive-shell.spec.ts`'s skip-target assertion and a11y behaviour survive.
- `topbar.ts|.html` — Sakai `layout-topbar` structure, but our content: `layout-menu-button`, brand (`logoUrl` + `title` inputs, `routerLink="/"`), then `layout-topbar-actions` containing a dark-mode toggle (`pi-moon`/`pi-sun`) and an `<ng-content select="[shell-actions]">` slot so `agent-crm` keeps injecting the language switcher and sign-out. On small screens collapse the actions behind the `layout-topbar-menu-button` (`pi-ellipsis-v`) using PrimeNG `StyleClassModule`, as Sakai does.
- `sidebar.ts|.html` + `menu.ts` + `menu-item.ts` — ports of `app.sidebar.ts`, `app.menu.ts`, `app.menuitem.ts`. `menu.ts` renders `<ul class="layout-menu">` from an input, **not** a hardcoded model. `menu-item.ts` keeps the expand/collapse animation, `routerLinkActive` handling and `menuSource$` coordination.
- `footer.ts` — one line, product name + year.
- Extend the shell contract in place of `ShellNavigationItem`:

```ts
export interface ShellMenuItem {
  readonly label: string;
  readonly icon?: string;
  readonly routerLink?: string;
  readonly exact?: boolean;
  readonly items?: readonly ShellMenuItem[];
}
```

**Delete after migration:** `responsive-shell.ts|.html|.scss`. Update `src/frontend/projects/shared-ui/src/public-api.ts` to export the new layout API and drop `./lib/layout/responsive-shell`.

**`customer-portal` is a consumer** (`projects/customer-portal/src/app/shell/portal-shell.ts` imports `ResponsiveShell`). Port `portal-shell` to the new components mechanically — same nav entries, same behaviour, no visual redesign work beyond what the shared shell gives it. `customer-portal` must build and its specs must stay green.

### 3 — Global Sakai styles

**Create files under `src/frontend/projects/shared-ui/styles/layout/`** (shipped via `stylePreprocessorOptions.includePaths`, or imported by path from each app's `styles.scss`):

`_variables.scss` (Sakai `variables/_common|_light|_dark`), `_core.scss`, `_main.scss`, `_topbar.scss`, `_menu.scss`, `_footer.scss`, `_responsive.scss`, `_typography.scss`, `_utils.scss`, `_mixins.scss`, `layout.scss` (the `@use` barrel). Do **not** port `_preloading.scss`, `demo.scss`, `code.scss`, `flags.scss`.

**RTL conversion is mandatory while porting.** Sakai's layout SCSS has 44 physical declarations. Convert every one:

| Sakai | Port as |
|---|---|
| `_topbar.scss:7 left: 0` | `inset-inline-start: 0` |
| `_topbar.scss:76,110,146 margin-right` / `84,109 margin-left: auto` | `margin-inline-end` / `margin-inline-start` |
| `_topbar.scss:127 right: 2rem` | `inset-inline-end: 2rem` |
| `_menu.scss:11 left: 2rem` | `inset-inline-start: 2rem` |
| `_menu.scss:76 margin-right`, `81 margin-left: auto`, `105–130 margin-left` indent ladder | `margin-inline-end` / `margin-inline-start` |
| `_responsive.scss:5–6 margin-left/right: auto` | `margin-inline: auto` |
| `_responsive.scss:14,44,54,73 margin-left`, `15,55,74 padding-left` | `margin-inline-start` / `padding-inline-start` |
| `_responsive.scss:20,50,79,93 left: 0` | `inset-inline-start: 0` |
| `_responsive.scss:25 border-right` | `border-inline-end` |
| `_main.scss:7 transition: margin-left` | `transition: margin-inline-start` |

The sidebar off-canvas transform in `_responsive.scss` uses `translateX(-100%)`; make it direction-aware (`[dir='rtl'] … translateX(100%)` or a `--sc-offcanvas-sign` custom property set per direction). `_typography.scss:52 border-left` on blockquote → `border-inline-start`.

**File: `src/frontend/projects/agent-crm/src/styles.scss`** — replace the 20-line reset with the Sakai global entry: `@use` the ported `layout.scss`, keep `html, body { height: 100% }`, drop the old `font-family` stack in favour of Sakai's `_core.scss` (`html { font-size: 14px }`, body font + `--surface-ground` background). Mirror the same entry in `projects/customer-portal/src/styles.scss` so the two apps share one baseline. **Do not** add the Lato webfont — keep the existing system font stack to avoid a new network asset.

### 4 — Shell wiring in `agent-crm`

**File: `src/frontend/projects/agent-crm/src/app/shell/agent-shell.ts`** — keep `shellTitle`, `signOut()`, `branding.load()`, `authorization.load()`. Convert `navigationItems` from a flat list to grouped `ShellMenuItem[]`, preserving **every** existing permission gate, label key, icon and route:

| Group | Items (permission) |
|---|---|
| `agent.navigation.groups.overview` | Home (none) |
| `agent.navigation.groups.tickets` | Tickets (`tickets.view`), My Tickets (`tickets.view`), New Ticket (`tickets.create`) |
| `agent.navigation.groups.customers` | Customers (`customers.manage`) |
| `agent.navigation.groups.administration` | Roles (`roles.view`), Departments (`departments.view`), Branches (`branches.view`), Ticket Categories (`ticketcategories.view`), Ticket Priorities (`ticketpriorities.view`), Staff Users (`users.view`), System Configuration (`configuration.view`), Branding (`branding.view`), Audit (`audit.view`) |

Drop a group entirely when every child is filtered out — an empty group header must never render.

**File: `src/frontend/projects/agent-crm/src/app/shell/agent-shell.html`** — swap `<sc-responsive-shell>` for `<sc-app-layout>`, keeping the `shell-actions` projection (language switcher + sign-out).

**File: `src/frontend/projects/agent-crm/src/app/i18n/agent-translations.ts`** — add `agent.navigation.groups.*` keys in `en` and `ar`.

### 5 — Shared page patterns in `shared-ui`

Add only these four abstractions — they remove real duplication across 26 pages. **Do not** wrap PrimeNG components beyond this.

**Create file: `src/frontend/projects/shared-ui/src/lib/layout/page-header.ts|.html|.scss`** — `<sc-page-header [title] [subtitle]>` with an `[actions]` content slot; replaces all 26 `<header class="*-header">` blocks.

**Create file: `src/frontend/projects/shared-ui/src/lib/layout/page-container.ts|.scss`** — `<sc-page-container>` providing the single source of page padding/max-width; replaces all 26 `.*-page { padding: clamp(1rem, 4vw, 2rem) }` rules.

**Create file: `src/frontend/projects/shared-ui/src/lib/feedback/state-panel.ts|.html|.scss`** — `<sc-state-panel [state]="'loading' | 'empty' | 'error'" [message] [icon]>` with a retry-action slot; one consistent empty/loading/error presentation.

**Create file: `src/frontend/projects/shared-ui/src/lib/layout/detail-grid.ts|.scss`** — the `<dl>` term/description grid used by every detail screen, responsive one-column below ~48rem, using `padding-inline`/`margin-inline` only.

Export all four from `public-api.ts`.

### 6 — Screen migration (all 26 pages)

Apply in this order; the mechanical part of every page is identical:

- wrap in `<sc-page-container>` + `<sc-page-header>`;
- **delete the per-page `crm-agent-language-switcher`** (24 pages — it now lives in the topbar) and drop the now-unused import from each component's `imports` array;
- delete the page's bespoke header/padding SCSS, leaving only genuinely page-specific rules;
- replace ad-hoc loading/empty/error markup with `<sc-state-panel>`.

1. **Login** — `auth/login.html|.scss`: Sakai's centred card-on-gradient auth layout. Keep the language switcher here (there is no topbar on this route) and the existing form controls, validation and `data-testid`s.
2. **Forbidden** — `auth/forbidden.ts`.
3. **Home** — `home/home.html|.scss`: Sakai card grid.
4. **List pages** (`roles`, `departments`, `branches`, `staff-users`, `ticket-categories`, `ticket-priorities`, `system-configuration`, `audit`, `customers`, `tickets`, `my-tickets`): page header → `.sc-filter-toolbar` → `p-table` → paginator. Wrap each `p-table` in a PrimeNG `p-card`, set `[scrollable]="true"` where not already set, and use `p-tag` for status/priority/active columns. **Do not touch** `[lazy]`, `[paginator]`, `[rows]`, `[totalRecords]`, `[paginatorLocale]`, `(onLazyLoad)`, `dataKey`, sort fields or any service call.
5. **Form pages** (`role-form`, `role-permissions`, `department-form`, `branch-form`, `staff-user-form`, `staff-user-roles`, `ticket-category-form`, `ticket-priority-form`, `customer-form`, `ticket-create-form`, `system-configuration` form, `branding-settings`): one `p-card` per logical section, a `.sc-form-grid` two-column responsive grid collapsing to one column below ~48rem, labels above inputs, required marker, `p-message severity="error"` for validation, actions right-aligned (logical end) at the card footer. **Do not change** any `FormGroup`, validator, error key or submit handler.
6. **Customer detail** — `customers/customer-detail.html|.scss` (468 lines): identity header with status tags, then a `p-tabs` (PrimeNG 20 `p-tabs`/`p-tabpanel`) grouping Contacts / Notes / Attachments / Timeline instead of one long stack. Every existing action button and permission gate is preserved verbatim.
7. **Audit detail** — `audit/audit-detail.html`: `<sc-detail-grid>` + a `p-card` for the payload.

### 7 — Ticket Detail workspace (highest priority)

**File: `src/frontend/projects/agent-crm/src/app/tickets/ticket-detail.html` (368 lines) and `ticket-detail.scss` (128 lines)**

Replace the seven flat `<section class="ticket-detail-section">` blocks (lines 10, 88, 96, 126, 202, 303, 342) with a two-column Sakai workspace. **Keep every existing binding, `@if` permission gate, form group, action handler and translation key** — this is markup and CSS only.

```
<sc-page-container>
  <sc-page-header>  ticket number + subject, status p-tag, priority p-tag,
                    escalation-level p-tag, [actions]: Change status /
                    Assign / Escalate p-buttons (each keeping its current
                    permission @if)
  main column (≈2fr)
    p-card "Description"        — subject + description (was §identity)
    p-card "History"            — existing timeline (was §history, line 303)
  side column (≈1fr)
    p-card "State & ownership"  — status, assignment, department/branch,
                                  escalation state (was §routing + §escalation)
    p-card "Context"            — customer, category, subcategory, priority,
                                  channel, inactive-reference tags
                                  (was §customer + §classification)
    p-card "Timestamps"         — created/updated/resolved (was §timestamps)
</sc-page-container>
```

- The inline `statusForm`, assign form and escalation form move into a `p-dialog` triggered from the header actions (`changingStatus()` and friends become dialog visibility). Keep the existing signals, `submitStatusChange()`, `cancelStatusChange()`, `onStatusSelected()`, `statusReasonRequired()`, `statusErrorKey()` and `allowedStatusTransitions` logic untouched.
- Columns are a CSS grid using `grid-template-columns`; it collapses to one column below ~64rem — no physical-side CSS, so RTL mirrors automatically.
- `notFound()` renders `<sc-state-panel state="error">`.

### 8 — Old-design removal

After all screens are migrated and green:

- delete `responsive-shell.ts|.html|.scss` and its `public-api.ts` export (spec rewritten against `app-layout.ts`);
- delete the per-page header/padding/filter SCSS rules superseded by `sc-page-container` / `sc-page-header` / `.sc-filter-toolbar` across the 26 page SCSS files — a page whose SCSS becomes empty loses its file and its `styleUrl`;
- drop the now-unused `AgentLanguageSwitcher` imports from the 24 migrated page components (the component itself stays — the topbar and login use it);
- run `` grep -rn 'responsive-shell\|sc-responsive-shell' src/frontend `` and `` grep -rn '\-page {' src/frontend/projects/agent-crm/src `` to prove nothing dangles.

**Do not** remove anything outside this list.

---

## No backend changes required.

No controller, service, DTO, migration, permission or API contract is touched. If a screen appears to need data the API does not return, keep current product behaviour and record it in the completion report — do not add a backend field.

---

## Edge Cases & Failure Modes

- **Empty menu group** — an agent holding only `tickets.view` must see the Tickets group and no Administration header. Enforced in `agent-shell.ts` `navigationItems` by filtering groups whose `items` array is empty.
- **RTL sidebar off-canvas** — `_responsive.scss` slides the sidebar with `translateX(-100%)`; unconverted it slides the wrong way in Arabic. Enforced by the direction-aware transform in task 3.
- **RTL menu indentation** — `_menu.scss:105–130` indents nested items with `margin-left`; in RTL that indents away from the menu edge. Enforced by the `margin-inline-start` conversion.
- **Dark mode + PrimeNG cssLayer** — `darkModeSelector: '.app-dark'` must match the class `LayoutService` writes; a mismatch yields dark surfaces with light component internals. Verify both halves after task 1.
- **Dark-mode persistence with a stale/blocked `localStorage`** — reads must fall back to light without throwing (private-browsing and quota cases), mirroring `LocaleService`.
- **Component style budget** — `anyComponentStyle` errors at **8 kB**. `ticket-detail.scss` and `customer-detail.scss` are the largest; keep shared rules in the global layer rather than growing per-component SCSS.
- **Initial bundle budget** — already **577.71 kB** raw against a 500 kB warning / 1 MB error. New shell components are eagerly loaded; keep dialogs and `p-tabs` inside lazy feature chunks and re-check after each milestone.
- **`customer-portal` breakage** — it imports `ResponsiveShell` and `providePrimeNgPlatform`. Deleting the shell without porting `portal-shell.ts` breaks its build; port it in task 2, not at the end.
- **Overlay/dialog direction** — PrimeNG overlays attach to `body`; `dir` is on `<html>` so they inherit, but the ticket-detail and customer-detail dialogs must be checked in Arabic for mirrored close buttons and footer action order.
- **Sticky topbar over tables** — `.layout-topbar` uses `position: fixed`; scrollable `p-table` headers must not overlap it. Verify at 768px.
- **Existing spec selectors** — `responsive-shell.spec.ts` asserts `main[tabindex="-1"]`, `[data-testid="mobile-menu-trigger"] button` and `.sc-mobile-navigation`. Keep the first two on the new layout; `.sc-mobile-navigation` disappears with the drawer and that assertion is replaced by the `layout-mobile-active` container-class assertion.
- **`my-tickets.spec.ts`** asserts `.my-tickets-error`. If that markup becomes `<sc-state-panel>`, keep the class on the host or update the spec in the same commit.

---

## Test Plan

1. **Rewrite** `src/frontend/projects/shared-ui/src/lib/layout/responsive-shell.spec.ts` → `app-layout.spec.ts`: renders topbar/sidebar/footer; `<main tabindex="-1">` present; `[data-testid="mobile-menu-trigger"]` toggles `layout-mobile-active`; `NavigationEnd` closes the mobile menu; nested `ShellMenuItem` children render and expand.
2. **New** `src/frontend/projects/shared-ui/src/lib/layout/layout.service.spec.ts`: `onMenuToggle()` transitions static/overlay/mobile state; `isDarkTheme` toggling adds and removes `.app-dark` on the document element; a throwing `localStorage` falls back to light.
3. **New** `src/frontend/projects/shared-ui/src/lib/layout/page-header.spec.ts` and `feedback/state-panel.spec.ts`: title/subtitle/actions projection; each of the three states renders its message and the error state exposes its retry action.
4. **Extend** `src/frontend/projects/agent-crm/src/app/shell/*.spec.ts` (add `agent-shell.spec.ts` if absent): a user with only `tickets.view` sees the Tickets group and **no** Administration group; a user with `audit.view` sees Audit under Administration; sign-out still calls `AuthService.signOut()` and routes to `/login`.
5. **Preserve and re-run** all 31 existing `agent-crm` specs. Only selector-level edits are allowed; no spec may be weakened or deleted to accommodate markup. Follow the existing label-based query style (`By.css('p-button[label="Edit"]')`, `querySelector('h1')`).
6. **Update** `src/frontend/projects/agent-crm/src/app/tickets/ticket-detail.spec.ts` for the dialog-based status/assign/escalation flows — same assertions on behaviour and permission gating, new trigger path.
7. **Regression** `customer-portal` specs unchanged and green after `portal-shell` is ported.

---

## Verification Steps

1. **Frontend tests:** `cd src/frontend && npm test` — full workspace suite, zero failures.
2. **Lint:** `cd src/frontend && npm run lint`.
3. **Format:** `cd src/frontend && npm run format:check`.
4. **Production build:** `cd src/frontend && npm run build:agent-crm` and `npm run build:customer-portal`.
5. **Bundle size:** record `Initial total` before/after. **Baseline measured on this branch: 577.71 kB raw / 134.24 kB transfer** (`main` 183.68 kB, `polyfills` 34.59 kB, `styles` 15.05 kB). Report the delta; investigate any growth above ~40 kB raw.
6. **Runtime visual check** (`npm run start:agent-crm`) for Login, shell, a list page (Tickets), a form page (Ticket create), Customer detail and Ticket detail, in four combinations: **Desktop EN/LTR**, **Desktop AR/RTL**, **Mobile EN/LTR**, **Mobile AR/RTL**, at ~1280px / ~768px / ~390px. Check for horizontal overflow, clipped content, mirrored icons/menus, sidebar behaviour, overlay placement, permission-gated nav and actions, and console errors.
7. **Regression:** sign in, filter and paginate a ticket list, create a ticket, assign it, change its status, escalate it, open its history — all unchanged.

---

## Done Criteria

- [ ] Sakai preset, tokens and typography applied through `provide-prime-ng-platform.ts`; PrimeNG 20.4 / `@primeng/themes` / Angular 20.3 unchanged; no new runtime dependency added.
- [ ] Sakai shell (`layout-wrapper` / `layout-topbar` / `layout-sidebar` / `layout-main-container` / footer / mask) is the only shell; `responsive-shell.*` deleted and `customer-portal` ported.
- [ ] Navigation is grouped and every one of the 13 permission gates is preserved; empty groups never render.
- [ ] Dark mode toggles via `.app-dark`, persists, and degrades safely when storage is unavailable.
- [ ] All 26 Agent CRM screens use `sc-page-container` + `sc-page-header`; list pages follow header → filters → table → paginator; forms follow one layout; detail pages use `sc-detail-grid`.
- [ ] Ticket Detail is a two-column workspace with header actions and dialog-based status/assign/escalate flows; all gates and backend calls unchanged.
- [ ] Per-page language switchers removed from the 24 in-shell pages; the topbar and login keep one.
- [ ] Obsolete page SCSS and the old shell are deleted; no `responsive-shell` or superseded `*-page` rule remains.
- [ ] EN/LTR and AR/RTL verified for shell, sidebar, topbar, menus, page headers, forms, tables, pagination, dropdowns, dialogs, overlays, detail layouts, action areas and mobile navigation; no physical left/right in new CSS.
- [ ] No horizontal page overflow at 1280px / 768px / 390px.
- [ ] `npm test`, `npm run lint`, `npm run format:check`, both production builds pass.
- [ ] Bundle-size delta recorded against the 577.71 kB / 134.24 kB baseline.
- [ ] No backend, API, validation or business-rule change in the diff.

**STOP HERE. Report to the user and wait for confirmation before merging.**
