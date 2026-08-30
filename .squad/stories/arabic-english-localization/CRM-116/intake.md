# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/arabic-english-localization/CRM-116/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Arabic & English Localization
- **Feature slug (folder under `plans/`):** `arabic-english-localization`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CRM-116` *(used in filenames and plan tables; fill manually if empty)*
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** `(none)`
- **Milestone:** Sprint 1 — Security, Administration & Platform Foundation
- **Priority:** High
- **Parent epic:** CRM-109 [Epic] Platform User Experience & Configuration

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
Arabic & English Localization
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
## User Story

As a CRM user, I want to use the system in Arabic or English so that navigation, forms and business content are understandable in my preferred language.

## Business Rules

* Translation keys are stable identifiers; feature modules own their feature translations while shared UI owns common resources.
* User-entered customer/ticket content is not automatically translated by this platform feature.
* Server/application error contracts expose stable error codes; frontend maps them to localized messages rather than depending on English backend text.
* Layout direction is derived from active locale and applies to dialogs, menus, tables, forms and navigation.
* Arabic/English business fields are stored independently when both are required; one language must never silently overwrite the other.

## Fields Dictionary

| Field | Type | Required | Rules |
| -- | -- | -- | -- |
| SupportedLocale | enum | Yes | `ar` or `en` for initial scope |
| PreferredLocale | enum | No | Persisted for user/session/device according to context |
| Direction | derived enum | System | `rtl` for Arabic, `ltr` for English |
| TranslationKey | string | Yes | Stable resource identifier |
| ArabicValue | string | Conditional | Required for bilingual business reference/content where specified |
| EnglishValue | string | Conditional | Required for bilingual business reference/content where specified |
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
* Agent CRM and Customer Portal support Arabic and English UI resources through one shared localization approach.
* User can switch language and the preference persists according to the authenticated/anonymous context.
* Arabic switches the application shell/components to RTL and English to LTR without page-specific hacks.
* Navigation, shared validation/errors, dates/numbers and common UI text are localized consistently.
* Business reference/content models that require localization use explicit Arabic/English fields or the agreed localization model rather than embedding translated UI strings in code.
* Missing translations fall back predictably and are detectable during development/testing.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
None.

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CRM-104 — Done and merged. CRM-116 blocks CRM-117, CRM-118, CRM-119, CRM-120, CRM-131, CRM-132, CRM-145, CRM-158, CRM-162 and CRM-172; do not implement those stories.
- **Depends on code areas or other stories:** CRM-104 established both Angular applications, `@squad-crm/platform`, `@squad-crm/shared-ui`, runtime locale configuration and `LocaleService` with persisted `sc.locale` plus global `<html lang>` / `<html dir>` updates. CRM-112 added current Agent CRM login and role-management screens that contain the present user-facing copy.

## Extra notes (optional)

- ADR-010 is the accepted baseline: Arabic/English, global RTL/LTR and responsive web through reusable conventions.
- Reuse the existing `LocaleService` and Angular signal patterns. Apply YAGNI: do not add a third-party localization framework when the current two-locale/static-resource scope can be served by the platform library.
- Treat the browser/device `sc.locale` preference as the currently available anonymous and authenticated persistence context; no user-profile locale API exists in the merged repository and adding one would cross the StaffIdentity boundary without a stated contract.
- Localize all currently shipped application UI: shared home/smoke surfaces plus Agent CRM login and role screens. Feature dictionaries live with their feature; shared UI owns common resources without depending on `@squad-crm/platform`. Each application composition root bridges platform locale state into platform-neutral shared-ui inputs/outputs and the PrimeNG locale adapter.
- Add predictable English fallback with development/test diagnostics for missing keys, locale-aware date/number formatting helpers, stable error-code mapping, and an explicit bilingual-value type/validation primitive. No business reference/content entity currently requires database persistence in this story.

## Technical hints (optional)

- Primary implementation root: `src/frontend/`. Relevant files are the existing platform i18n primitives, both app homes, Agent CRM auth/role components, app providers, and their focused tests.
- PrimeNG remains the primary component library. Direction must remain document-global so its overlays inherit RTL/LTR without page-specific code.
- Required verification: focused localization/component unit tests, frontend lint, format check, production builds for both apps, and browser verification of English/Arabic copy, persisted switching, and document direction.

## Out of scope

- CRM-117 application shell/navigation implementation.
- CRM-118/119/120/131/132/145/158/162/172 feature work or translations for screens that do not yet exist.
- Automatic translation of user-entered content.
- A backend user-preference endpoint or StaffIdentity persistence change not specified by this story.
- A generic multilingual content persistence framework or database migration when no current business model consumes it.
- Responsive redesign beyond preserving current behavior; CRM-117 owns the real shell.
