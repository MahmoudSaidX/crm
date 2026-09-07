# configure-ticket-categories — plan overview

Entry point for the **configure-ticket-categories** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 131 | [`131-story-crm-131-configure-ticket-categories.md`](131-story-crm-131-configure-ticket-categories.md) | Configure Ticket Categories | CRM-131 | CRM-118 (Department CRUD pattern) |

## Dependency notes

First story of the Ticket Management epic (CRM-130). Depends on the existing
`DepartmentManagement.Contracts` `IDepartmentActiveLookup` for validating the optional
`DefaultDepartmentId`. No Ticket entity exists yet; later CRM-130 stories will consume this
category catalog.
