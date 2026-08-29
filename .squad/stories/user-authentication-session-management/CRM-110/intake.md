# Story intake — CRM-110

## Feature

- **Tracker:** CRM-110
- **Title:** User Authentication & Session Management
- **Epic:** CRM-108 — Security & Administration
- **Status:** In Progress
- **Priority:** Urgent

## Requirements

- Active staff users sign in/out with securely hashed passwords.
- Short-lived access credentials and revocable, refreshable sessions.
- Backend and Agent CRM protected routes reject anonymous callers.
- Deactivated users cannot establish or retain access.
- Authentication events are recorded without secrets; auth/password endpoints are rate-limited; production requires HTTPS.

## Reconciliation

- CRM-104, CRM-105, CRM-106 and CRM-204 are Done and merged.
- ADR-004 requires distinct staff/customer identity and backend-enforced revocable sessions.
- CRM-204 provides the authorization/current-user extension point and safe API error baseline.
- The existing Angular PrimeNG theme and RTL/LTR platform are the applicable Design System v1 baseline; no separate design artifact is attached to CRM-110.

## Scope boundary

- Staff authentication only. No customer identity, user administration, roles, permissions, memberships, branding or application-shell work.
