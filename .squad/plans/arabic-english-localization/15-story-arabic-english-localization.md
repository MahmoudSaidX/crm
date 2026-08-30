# Story 15 — Arabic & English Localization (Story: CRM-116)

## Prerequisites

- Story 02 / CRM-104 is merged: reuse its Angular platform, runtime config, `LocaleService`, and global document-direction foundation.
- Story 14 / CRM-112 is merged: preserve its role-management behavior while replacing shipped UI literals with owned translation resources.
- Follow `docs/adr/ADR-010-localization-responsive.md`; no backend or database changes are required.

---

## Story Goal

Provide one lightweight runtime localization approach for Agent CRM and Customer Portal. Users can select Arabic or English, retain that browser/device preference, and receive globally correct RTL/LTR behavior. Current navigation/action copy, validation, server error codes, PrimeNG common strings, dates and numbers must use stable localized resources with deterministic English fallback and development/test diagnostics.

Do not add translations for unimplemented downstream features, automatic translation, a user-profile persistence API, a database localization framework, or the CRM-117 application shell.

---

## Context — Read These Files First

1. `docs/adr/ADR-010-localization-responsive.md` lines 1–13 — preserve the accepted Arabic/English and global RTL/LTR baseline.
2. `src/frontend/projects/platform/src/lib/i18n/locale.service.ts` lines 1–75 — extend the existing signal-backed locale owner; keep `sc.locale`, runtime allow-list validation and document-global `lang`/`dir` updates.
3. `src/frontend/projects/platform/src/lib/i18n/locale.ts` lines 9–24 and `src/frontend/projects/platform/src/public-api.ts` lines 5–14 — reuse/export the existing `SupportedLocale` and `Direction` contract.
4. `src/frontend/projects/shared-ui/src/lib/theme/provide-prime-ng-platform.ts` lines 1–30 — keep PrimeNG configuration centralized and synchronize its common translations with the active locale.
5. `src/frontend/projects/agent-crm/src/app/auth/login.ts` lines 11–58 and `login.html` lines 1–58 — current unauthenticated form and shared validation/error copy.
6. `src/frontend/projects/agent-crm/src/app/roles/role-list.ts` lines 8–48, `role-list.html` lines 1–61, `role-form.ts` lines 11–102 and `role-form.html` lines 1–40 — current feature-owned text and stable backend codes `roles.duplicate_name` / `roles.duplicate_code`.
7. `src/frontend/projects/agent-crm/src/app/home/home.ts` lines 7–36 and `src/frontend/projects/customer-portal/src/app/home/home.ts` lines 5–27 — replace duplicated locale-toggle behavior with a shared accessible control while retaining the temporary CRM-104 smoke surfaces.
8. `src/frontend/projects/agent-crm/src/app/roles/role-list.spec.ts` lines 6–59 and `role-form.spec.ts` lines 7–118 — match existing standalone-component Jasmine/TestBed patterns.
9. `../angular-workspace-agent-crm-customer/02-story-angular-workspace-agent-crm-customer-portal.md` — CRM-104 precedent and explicit handoff of translation content to CRM-116.

---

## Product rules (from story)

- Translation keys are stable identifiers. Platform owns common resources; each app/feature owns its resources and registers them at bootstrap.
- English is the fallback locale. A missing active-locale value falls back to English; a missing English key returns the key and emits a development/test diagnostic.
- User-entered role/customer/ticket values remain verbatim and are never translated.
- Error UI maps stable backend `code` values, never backend English text.
- Explicit bilingual values retain independent `arabicValue` and `englishValue` properties.

---

## Frontend Tasks

### 1 — Add shared localization contracts and resource engine

**Create files:** `src/frontend/projects/platform/src/lib/i18n/localization.ts`, `localization.service.ts`, `localization.service.spec.ts`, `bilingual-value.ts`, `bilingual-value.spec.ts`.

- Define typed `TranslationKey`, `TranslationDictionary`, `TranslationResources` (`en` and `ar`), and a multi-provider registration helper `provideTranslations(resources)`.
- Implement a root `LocalizationService` that reads `LocaleService.locale`, merges registered resource objects, exposes `translate(key)`, `formatDate(value, options?)`, and `formatNumber(value, options?)`, and uses `Intl.DateTimeFormat` / `Intl.NumberFormat` with the active locale.
- Enforce deterministic fallback: active locale → English → stable key. Emit `console.warn` only when the English fallback key is also missing so development/tests can detect incomplete resources without breaking production UI.
- Define explicit `BilingualValue { arabicValue: string; englishValue: string }` plus a small validation/type-guard function requiring both independently non-blank. Do not add persistence or a generic content model.
- Export these APIs from `projects/platform/src/public-api.ts`. Update stale CRM-104 comments in `locale.ts` / `locale.service.ts`.

### 2 — Add platform-neutral common resources, language control and PrimeNG adapter

**Create files:** `src/frontend/projects/shared-ui/src/lib/i18n/common-translations.ts`, `language-switcher.ts`, `language-switcher.html`, `language-switcher.spec.ts`, `prime-ng-locale-adapter.ts`, `prime-ng-locale-adapter.spec.ts`.

**Modify files:** `src/frontend/projects/shared-ui/src/lib/theme/provide-prime-ng-platform.ts`, `src/frontend/projects/shared-ui/src/public-api.ts`.

- Own stable common keys (language names/actions, common validation/action/status text) in shared-ui as a platform-neutral structural resource object; **do not import `@squad-crm/platform` from shared-ui**.
- Implement one repeated PrimeNG-first `sc-language-switcher` with plain locale/label/accessibility inputs and a locale-change output. It must not inject platform services.
- Expose a platform-neutral PrimeNG locale adapter that accepts `en` / `ar` and calls PrimeNG's existing `PrimeNG.setTranslation()`. Supply the common PrimeNG labels required by shipped components, owned in shared-ui.
- Keep document direction solely in `LocaleService`; add no component/page direction conditionals.

### 3 — Register app/feature resources and localize shipped surfaces

**Create files:** `src/frontend/projects/agent-crm/src/app/i18n/agent-translations.ts`, `agent-language-switcher.ts`, `src/frontend/projects/customer-portal/src/app/i18n/portal-translations.ts`, `portal-language-switcher.ts`, `src/frontend/projects/agent-crm/src/app/roles/role-translations.ts`.

**Modify files:** both `app.config.ts` files; both home `.ts` / `.html` files; Agent CRM `auth/login.ts` / `login.html`; role list/form `.ts` / `.html` files and their focused specs.

- Register common and app/feature resources in each root `ApplicationConfig`; keep feature-owned role keys in the roles feature file even though registration happens at bootstrap. Each application config owns an initializer/effect bridging `LocaleService.locale` into the shared-ui PrimeNG locale adapter.
- Add thin app-owned language-switcher composition components that bind `LocalizationService` copy and `LocaleService` state to the platform-neutral shared-ui control. Agent screens reuse the Agent wrapper; Portal reuses its Portal wrapper.
- Replace every shipped UI literal on the two home surfaces, login and role-management screens with stable keys resolved through `LocalizationService`. Do not translate role names, codes or descriptions returned by the API.
- Add the shared language switcher to both homes, login, role list and role form so anonymous and current authenticated routes can change locale before CRM-117 supplies a shell.
- Replace `RoleForm.resolveDuplicateField()`'s presentation-specific state with stable error-code mapping to localized keys. Unknown errors use a common generic error key; never render backend `message` text.
- Keep data/test identifiers, routes, form validators, service calls and role lifecycle behavior unchanged.

### 4 — Document the completed convention

**Modify file:** `src/frontend/README.md`.

- Replace the “foundation only” wording with concise ownership/registration/fallback/formatting/error-code guidance and explain `sc.locale` persistence plus global direction behavior.
- State that bilingual business values are explicit and user-entered content is not automatically translated.

**No backend changes required.**

---

## Edge Cases & Failure Modes

- Unsupported/stale stored locale: `LocaleService.initialize()` falls back to runtime `defaultLocale`; existing locale tests remain green.
- Storage unavailable: switching still updates signals and document direction; persistence failure remains non-fatal in `locale.service.ts`.
- Arabic key absent: `LocalizationService.translate()` returns the English value. English key absent too: return the stable key and warn once per missing key in development/test.
- Resource collision: later app/feature registration may not silently overwrite a different value for the same locale/key; detect and throw during service resource merge so ownership conflicts fail tests/bootstrap.
- Locale changes after component creation: templates call the signal-aware translation service, and PrimeNG synchronization reacts to the same locale signal.
- Unknown backend error: show a localized generic error. Never render backend detail/message.
- User-entered/business text: render values verbatim; translation functions only receive stable keys.
- Arabic/English bilingual values: validation rejects either blank field and never substitutes one field for the other.

---

## Test Plan

1. Unit — `localization.service.spec.ts`: active locale lookup, Arabic→English fallback, missing-English key diagnostic/key fallback, resource collision, and locale-aware date/number output.
2. Unit — `bilingual-value.spec.ts`: accepts two independent non-blank values and rejects either missing/blank field.
3. Unit — `language-switcher.spec.ts`: plain input copy renders and activating the control emits the next locale without any platform dependency. App wrapper/component tests cover persisted direction changes.
4. Unit — `prime-ng-locale-adapter.spec.ts`: supplying English/Arabic calls `PrimeNG.setTranslation()` with matching common labels; app integration supplies the active locale.
5. Component — extend both home specs and role list/form specs; add `login.spec.ts` if absent. Assert representative English and Arabic labels, stable error-code mapping, and unchanged role/user-entered values.
6. Regression — run all frontend tests to ensure existing locale/config/auth/role behavior remains green.

---

## Verification Steps

1. **Frontend tests:** from `src/frontend`, run `npm test`.
2. **Frontend lint:** from `src/frontend`, run `npm run lint`.
3. **Frontend format:** from `src/frontend`, run `npm run format:check`.
4. **Production builds:** from `src/frontend`, run `npm run build` for both applications.
5. **Browser verification:** serve both apps; verify English and Arabic copy, persisted refresh, `<html lang>` / `<html dir>`, role values remaining verbatim, and representative PrimeNG control/overlay direction at desktop and mobile viewport widths.
6. **Scope regression:** inspect `git diff -- . ':(exclude).codex'`; confirm no backend/schema/module changes, no downstream shell/features, no secrets/debug code, and no `.codex/` changes.

---

## Done Criteria

- [ ] Agent CRM and Customer Portal register resources through the same platform localization service.
- [ ] Users can switch between Arabic and English on current anonymous/authenticated routes; browser/device preference persists.
- [ ] Arabic produces document-global RTL and English LTR with no page-specific direction hacks.
- [ ] Current common/navigation/action/form/validation/error copy and PrimeNG common labels are localized consistently.
- [ ] Dates and numbers have shared active-locale formatting helpers with tests.
- [ ] Stable backend error codes map to localization keys; backend English messages are not used.
- [ ] Feature resources remain feature-owned and shared resources remain shared-owned.
- [ ] Missing Arabic translations fall back to English; fully missing keys are detectable and deterministic.
- [ ] Explicit bilingual-value validation keeps Arabic and English independent; no automatic translation exists.
- [ ] Frontend tests, lint, format check, production builds and browser verification pass.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 16.**
