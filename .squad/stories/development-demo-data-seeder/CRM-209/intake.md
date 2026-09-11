# Story intake

- Folder: `.squad/stories/development-demo-data-seeder/CRM-209/intake.md`

---

## Feature

- **Feature name (display):** Development Demo Data Seeder
- **Feature slug (folder under `plans/`):** `development-demo-data-seeder`

## Tracker (metadata only)

- **Tracker type:** `linear`
- **Work item id:** `CRM-209` *(https://linear.app/mahmoud-said/issue/CRM-209/seed-realistic-demo-data-for-local-development — created after implementation: Linear was unreachable during intake (`CONNECTION_CLOSED`), so the story was written from this intake and reconciled against it afterwards.)*
- **Work item type:** `Story`
- **Status:** `In Progress`
- **Assignee:** `Mahmoud Said`
- **Labels:** `developer-experience`

---

## Title

```
Seed Realistic Demo Data for Local Development
```

---

## Description

```
## Problem

A freshly migrated local Squad CRM database is empty. Making it usable today requires
running the StaffIdentity bootstrap per account, the RoleManagement administrator
bootstrap, and then hand-creating departments, branches, categories, priorities,
customers and tickets through the UI or HTTP. There is no realistic dataset for
development, demos, manual testing or UI verification, and `scripts/seed` only applies
the ArchitectureFixture SQL fixture (no customer/ticket data).

## Desired Behavior

An explicitly invoked demo-data seeder that makes a fresh local environment immediately
usable:

    docker compose up --build
    ./scripts/migrate
    ./scripts/seed-demo

It seeds only capabilities that exist today: StaffIdentity users, RoleManagement
roles/permission grants/assignments, departments, branches, ticket categories, ticket
priorities, customers (profiles, contacts, notes), and tickets with realistic
assignment/status/escalation history spread over the previous ~90 days.

## Safety

- Explicitly invoked only. Never on application startup, never after migrations.
- Refuses to execute in Production — enforced in code, not only documented.
- Idempotent and deterministic; converges on a second run, no duplicate rows.
- Respects DB constraints, module ownership boundaries and domain rules.
- No DELETE/TRUNCATE/DROP; coexists with existing developer data.
- Never logs passwords or secrets; contains no production credentials; does not change
  production bootstrap semantics.

## Module ownership

No central seeder may reach into every module's private DbContext. Each module owns its
own demo-data contributor; the CLI orchestrates contributors in dependency order.

## Demo personas (Development only)

| Email | Display name | Role |
| --- | --- | --- |
| admin@squadcrm.local | Ahmed Admin | Administrator |
| manager@squadcrm.local | Sara Manager | Support Manager |
| agent1@squadcrm.local | Omar Hassan | Support Agent |
| agent2@squadcrm.local | Nour Khaled | Support Agent |
| viewer@squadcrm.local | Youssef Viewer | Read Only |

Demo password: `SquadDemo!2026`, overridable through `SQUADCRM_DEMO_PASSWORD`. Demo-only,
never logged, hashed through the existing StaffIdentity password hasher.

## Roles and permissions

Derive grants from the live `permission_definition` catalog — never a hardcoded duplicate
catalog. Administrator gets the full current catalog. Support Manager gets customer and
ticket operational capabilities without system administration. Support Agent gets the
customer/ticket capabilities needed for support work. Read Only gets view permissions
only, no mutation.

## Organizational, customer and ticket data

~5 departments, ~8 Saudi-oriented branches, realistic Arabic/English categories and the
four priorities. Dataset sizes: small (~20 customers / ~50 tickets), medium default
(~150 / ~400), large (~1000 / ~5000). Customers ~80-90% Active, mixed preferred
languages, synthetic contacts and notes. Tickets use realistic Arabic/English subjects,
a non-uniform status distribution, at least 30-50 currently assigned tickets per demo
agent, some unassigned, and realistic history: assignment, reassignment, status change,
escalation — only via the authoritative `TicketStatusTransitions` rules, no illegal
transitions, no escalation of Resolved/Closed tickets.

## Determinism

Fixed random seed `20260911`. Deterministic business keys, not hardcoded GUIDs where
business keys suffice.

## Documentation

Update local-development documentation with the three-command workflow plus a clearly
marked `DEVELOPMENT DEMO ACCOUNTS` section stating NEVER USE THESE CREDENTIALS IN
PRODUCTION.
```

---

## Acceptance criteria

```
1. Fresh migrated Development database + seed succeeds.
2. Second seed run succeeds with no duplicate data.
3. Production environment refuses execution (enforced in code).
4. All five demo users resolve correctly and can log in.
5. Administrator holds the full current permission catalog.
6. Agent and viewer permission differences are correct (viewer has no mutation grants).
7. Customer counts and relationships are valid for the selected size.
8. Ticket counts and relationships are valid for the selected size.
9. Assigned-to-me data exists for both demo agents (>= 30 each at medium).
10. Ticket statuses are distributed across all six lifecycle statuses.
11. Assignment history exists (including reassignments).
12. Status history exists.
13. Escalation history exists, with level 1 and higher levels, agent and department targets.
14. Ticket timeline renders meaningful histories.
15. Existing migrations still work from a fresh database.
16. Existing affected backend tests pass; `dotnet format --verify-no-changes` clean.
17. Live HTTP smoke with seeded users: login admin/agent1/viewer, browse customers,
    browse tickets, open ticket detail, My Tickets as agent1, and a mutation-permission
    difference between agent and viewer.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** None.
- **Depends on code areas or other stories:** `src/backend/src/Tools/SquadCrm.RoleManagement.Bootstrap` and `src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity.Bootstrap` (existing bootstrap precedent); `scripts/migrate`, `scripts/seed`, `scripts/reset`; RoleManagement permission catalog; CustomerManagement and TicketManagement persistence and domain rules.

## Extra notes (optional)

- Linear was unreachable during intake (`CONNECTION_CLOSED`); `.squad/` and the code were the sources of truth used. Linear story CRM-209 was created once connectivity returned and this intake was reconciled against it — the implementation was not regenerated.
- Existing `scripts/seed` (ArchitectureFixture SQL fixture) stays untouched — the demo seeder is a separate command.

## Technical hints (optional)

- Backend: ASP.NET Core modular monolith, EF Core/PostgreSQL, schema per module.
- Architecture tests forbid a module project referencing another module's implementation
  project; a project under `src/Tools/` may reference module implementations (existing
  precedent: `SquadCrm.RoleManagement.Bootstrap`).
- `Ticket.Create/Assign/ChangeStatus/Escalate` all take an explicit timestamp, so
  historical backdating is possible through the domain methods. `TicketService` is
  `internal` and stamps `DateTimeOffset.UtcNow`, so it cannot produce a 90-day history.

## Out of scope

- UI redesign, Sakai migration changes.
- New CRM capabilities (SLA, automation, communications, knowledge base, portal, AI, reporting).
- A generic seeding framework or a generic CLI framework.
- Faker or other data-generation libraries.
- Destructive demo reset inside the normal seed command.
- Production bootstrap or production security changes.
- Automatic seeding on startup or after migrations.
