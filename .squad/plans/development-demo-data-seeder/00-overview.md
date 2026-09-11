# development-demo-data-seeder — plan overview

Entry point for the **development-demo-data-seeder** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 143 | [`143-story-crm-209-development-demo-data-seeder.md`](143-story-crm-209-development-demo-data-seeder.md) | Development Demo Data Seeder | `CRM-209` | Stories for StaffIdentity, RoleManagement, Department/Branch, Customer and Ticket Management (all merged) |

## Dependency notes

- Backend + scripts + docs only. No frontend change, no new CRM capability, no migration.
- Seeds only capabilities that exist today: StaffIdentity users, RoleManagement roles/grants/assignments, departments, branches, ticket categories, ticket priorities, customers (profile, contacts, notes), tickets (+ assignment/status/escalation history and outbox rows).
- Existing `scripts/seed` (ArchitectureFixture SQL fixture) is untouched; `scripts/seed-demo` is a separate explicit command. `scripts/reset` keeps owning destructive reset.
- Linear was unreachable during intake (`CONNECTION_CLOSED`). Linear story [CRM-209](https://linear.app/mahmoud-said/issue/CRM-209/seed-realistic-demo-data-for-local-development) was created once connectivity returned, and the intake and this plan were reconciled against it; the plan was not regenerated.
