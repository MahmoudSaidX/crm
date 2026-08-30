# arabic-english-localization — plan overview

Entry point for the **arabic-english-localization** feature.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 15 | [15-story-arabic-english-localization.md](15-story-arabic-english-localization.md) | Arabic & English Localization | CRM-116 | Story 02 (Angular workspace, CRM-104) |

## Dependency notes

- CRM-104 already supplies the two Angular applications, the platform/shared-ui libraries, runtime locale configuration, persistent `LocaleService`, and document-global direction handling.
- CRM-112 supplies the current Agent CRM login and role screens whose shipped UI text is localized here.
- CRM-117 and the other stories blocked by CRM-116 remain out of scope.
