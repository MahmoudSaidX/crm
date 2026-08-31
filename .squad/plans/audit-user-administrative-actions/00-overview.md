# Feature: Audit User and Administrative Actions

| Story | CRM | NN |
|---|---|---|
| Audit User and Administrative Actions | CRM-114 | 19 |

## Dependency notes

- **Depends on** Story 17 (`../configure-role-permissions/17-story-crm-113.md`, CRM-113 — role/permission/authorization foundation this story's `audit.view` permission and policy plug into) and Story 18 (`../manage-staff-users/18-story-crm-111-manage-staff-users.md`, CRM-111 — `StaffIdentity.Contracts`' `IStaffSubjectReferenceReader` is consumed by `AuthorizationBootstrapService`, the one operation wired end-to-end in Story 19). Neither `StaffUserService` nor `PermissionService` is touched by Story 19 — per the user's explicit decision, the new `IAuditRecorder` is wired only into `RoleManagement`'s `AuthorizationBootstrapService`, whose additive constructor dependency and post-write call are described in Story 19.
- **Risk classification: ARCHITECTURE** — this story adds a new shared/cross-cutting capability (`IAuditRecorder`) consumed by an existing module. Story 19 explicitly resolves the structural question flagged during intake (relationship to RoleManagement's/StaffIdentity's existing module-local audit classes, and which call site gets wired) and is routed to `arch-reviewer` before implementation.
- Implements the append-only audit-record acceptance criteria from the CRM-114 intake; does not amend any ADR. `docs/adr/ADR-005-events-outbox.md` is referenced only to justify **not** reusing the outbox mechanism here (no fan-out consumer exists for an audit write).
