# Squad CRM — Angular frontend workspace

Angular workspace hosting the two Squad CRM surfaces and the libraries they
share. Established by **CRM-104**.

Everything here is _foundation_: workspace, boundaries, runtime configuration,
HTTP plumbing, PrimeNG integration and the Arabic/English direction mechanism.
Feature screens, the real application shell (CRM-117), translation content
(CRM-116) and auth/session (CRM-110) are deliberately out of scope.

## Workspace layout

```
src/frontend/
├── angular.json
├── eslint.config.js          Lint rules + dependency-boundary enforcement
├── .prettierrc.json
├── package.json
└── projects/
    ├── agent-crm/            Application — internal agent CRM  (dev port 4200)
    │   ├── public/config.json    Runtime configuration for this surface
    │   └── src/app/<capability>/ Feature code lives per capability
    ├── customer-portal/      Application — customer portal     (dev port 4300)
    │   ├── public/config.json
    │   └── src/app/<capability>/
    ├── platform/             @squad-crm/platform   Runtime config, HTTP, locale/direction
    └── shared-ui/            @squad-crm/shared-ui  Shared PrimeNG presentation setup
```

Feature code lives under per-capability folders inside each application
(`projects/agent-crm/src/app/<capability>/…`). There is no global `features/`
folder by design.

## Prerequisites

- **Node.js ≥ 22.12** (pinned by this story; developed against v22.21.1).
- npm ≥ 10.

## Install and run

```bash
cd src/frontend
npm ci

npm run start:agent-crm        # http://localhost:4200
npm run start:customer-portal  # http://localhost:4300
```

| Script                                    | What it does                                                      |
| ----------------------------------------- | ----------------------------------------------------------------- |
| `npm run build:agent-crm`                 | Production build → `dist/agent-crm/`                              |
| `npm run build:customer-portal`           | Production build → `dist/customer-portal/`                        |
| `npm run build`                           | Both production builds in sequence                                |
| `npm run lint`                            | ESLint across every project, including boundary rules             |
| `npm run format` / `npm run format:check` | Prettier write / verify                                           |
| `npm test`                                | Karma + Jasmine, headless Chrome (CRM-202 owns the full strategy) |

## Dependency boundaries

Enforced by `no-restricted-imports` in `eslint.config.js` — `npm run lint`
fails on a violation.

| Project                | May depend on    | May **not** depend on                 |
| ---------------------- | ---------------- | ------------------------------------- |
| `@squad-crm/platform`  | Angular, RxJS    | `shared-ui`, any application, PrimeNG |
| `@squad-crm/shared-ui` | Angular, PrimeNG | `platform`, any application           |
| `agent-crm`            | both libraries   | `customer-portal` internals           |
| `customer-portal`      | both libraries   | `agent-crm` internals                 |

`platform` and `shared-ui` are **siblings, not layers**: neither may import the
other. `platform` owns runtime behavior and stays presentation-free; `shared-ui`
owns PrimeNG presentation and stays behavior-free. Applications are the only
place the two meet.

There is deliberately **no `util` library**. The locale/direction primitives are
plain, dependency-free TypeScript inside `platform`
(`projects/platform/src/lib/i18n/locale.ts`), because `LocaleService` is their
only consumer. Extract a `util` library later, when a second consumer gives it a
real reason to exist — not before.

Angular Material and other broad UI libraries (`@angular/cdk`, `@mui/*`,
`bootstrap`, `@ionic/angular`, `ng-zorro-antd`) are rejected by the same rule —
adding one requires a new ADR, per
[ADR-009](../../docs/adr/ADR-009-angular-primeng.md). See also
[docs/architecture/frontend.md](../../docs/architecture/frontend.md).

## Runtime configuration

Configuration is **runtime**, not build-time: the same production artifact is
promoted Dev → Test → UAT → Prod by swapping one static file. Each application
owns its own `config.json` because the two surfaces deploy independently; only
the contract is shared.

`projects/<app>/public/config.json`:

```json
{
  "apiBaseUrl": "http://localhost:5080",
  "defaultLocale": "en",
  "supportedLocales": ["en", "ar"],
  "appSurface": "agent-crm"
}
```

It is served publicly — **never put secrets in it.**

`providePlatform()` loads and validates the file before the application renders
and refuses to boot when `apiBaseUrl` is missing, empty or still a placeholder
such as `REPLACE_ME`, so a broken deployment fails loudly instead of silently
calling the wrong host.

### Mapping to the repository environment contract

[`env/frontend.env.example`](../../env/frontend.env.example) stays the
operator-facing contract. Deployment automation generates each surface's
`config.json` from it:

| Env variable                   | Runtime config field              |
| ------------------------------ | --------------------------------- |
| `AGENT_CRM_API_BASE_URL`       | `apiBaseUrl` of `agent-crm`       |
| `CUSTOMER_PORTAL_API_BASE_URL` | `apiBaseUrl` of `customer-portal` |
| `DEFAULT_LOCALE`               | `defaultLocale`                   |
| `SUPPORTED_LOCALES`            | `supportedLocales`                |

CRM-104 establishes and documents this contract. Generating the file in
containers/CI belongs to CRM-197 / CRM-203.

## HTTP

`provideHttpPlatform()` (from `@squad-crm/platform`) is the only frontend HTTP
bootstrap — applications must not call `provideHttpClient(...)` themselves. It
installs `apiBaseUrlInterceptor`, which prefixes relative request URLs with the
runtime `apiBaseUrl` and leaves absolute `http(s)://` URLs untouched.

Auth headers, token refresh, retries and business API clients are **not** here;
CRM-110 and the capability stories own those.

## PrimeNG

**Check PrimeNG before hand-rolling a control. Do not wrap every component.**
Wrap only repeated Squad CRM design/business/accessibility behavior.

`providePrimeNgPlatform()` in `@squad-crm/shared-ui` is the single place theme
and PrimeNG configuration live (a stock Aura preset plus the animations
providers PrimeNG overlays require). Brand palette, design tokens and any
dark-mode policy belong to later design work, not CRM-104.

PrimeIcons is loaded globally through the `styles` array in `angular.json`.

## Localization and direction

Foundation only — **full translation content is CRM-116.**

`LocaleService` (`@squad-crm/platform`) owns the active locale and derives the
document direction from it (`ar` → RTL, `en` → LTR), keeps `<html lang>` and
`<html dir>` in sync, and persists the choice under `sc.locale`. An unsupported
or stale persisted value falls back to the runtime `defaultLocale`.

The locale is applied inside the same bootstrap initializer that loads the
runtime configuration, so direction is correct before the first render — Angular
runs initializers concurrently, so two separate initializers could not guarantee
that ordering.

Each application's `index.html` ships `<html lang="en" dir="ltr">` as a static
default matching `en`, to avoid a flash of the wrong direction.

The `p-button` on each application's home route is a **temporary integration
smoke marker** proving PrimeNG, PrimeIcons and the direction mechanism work end
to end. CRM-117 replaces it with the real application shell.
