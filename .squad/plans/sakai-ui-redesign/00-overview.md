# sakai-ui-redesign — plan overview

Entry point for the **sakai-ui-redesign** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 142 | [`142-story-agent-crm-sakai-ui-redesign.md`](142-story-agent-crm-sakai-ui-redesign.md) | Agent CRM Sakai UI/UX redesign | — | Stories 15, 16, 141 |

## Dependency notes

- Frontend-only. Supersedes the shell from [`../responsive-application-shell/00-overview.md`](../responsive-application-shell/00-overview.md) (Story 16) and must not regress the RTL work from [`../arabic-english-localization/00-overview.md`](../arabic-english-localization/00-overview.md) (Story 15).
- Touches shared contracts in `projects/shared-ui` (`providePrimeNgPlatform`, the shell components), so `customer-portal` is ported mechanically in the same story.
- Visual reference: PrimeNG Sakai tag `20.0.0` (MIT), which matches our Angular 20 / PrimeNG 20. Sakai `master` (Angular 21) must not be used.
- No Tailwind, no ADR, no backend change.
