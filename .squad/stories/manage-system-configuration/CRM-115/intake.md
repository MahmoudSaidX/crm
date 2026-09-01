# CRM-115 — Manage System Configuration

## Deadline Acceptance Override

Implement a small allow-listed system-configuration store and admin screen
for a few settings actually needed by the demo. Typed validation and audit
are enough. Dynamic configuration distribution, secret management,
caching/invalidation frameworks, feature-flag platforms, and arbitrary
configuration extensibility are stretch/non-blocking.

## Acceptance Criteria

- Authorized admin can browse supported configuration keys with
  description, effective value and editability metadata.
- Editable values are validated by declared type/range/options before
  persistence.
- Configuration changes are audited with actor, key and safe before/after
  metadata.
- Sensitive configuration is represented by secret references / protected
  mechanisms; its raw value is never returned to the frontend/audit/logs.
- Runtime consumers use a canonical configuration service; whether a
  change is immediately effective or requires restart is explicit.
- Invalid configuration cannot leave the system in a partially applied
  state.

## Business Rules

- Only explicitly registered configuration keys are administratively
  editable; the UI cannot create arbitrary keys.
- Secrets are not normal configuration values.
- Key ownership stays with the owning capability; the platform provides
  shared storage/read/admin mechanics.
- Defaults are explicit and distinguishable from overridden values.

## Reconciliation notes

- No prior branch, `.squad/` plan, or commits existed for CRM-115 — clean
  slate.
- CRM-111 and CRM-113 were found already merged into `main` (commits
  `7c31e24`, `56fa9af`, `47cbd28`, `bdb32bd`) but Linear still showed
  Backlog/In Progress; both were moved to Done before this story started,
  per the `/next-story` reconciliation step.
- Blockers CRM-114 and CRM-113 are Done.
