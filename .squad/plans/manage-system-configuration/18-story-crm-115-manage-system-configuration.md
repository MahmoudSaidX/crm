# Plan — CRM-115 Manage System Configuration

Reuses the DepartmentManagement module shape (CRM-118) and the Audit
capability (CRM-114). No new architecture.

## Backend

- New module `SystemConfiguration` (schema `system_configuration`):
  - `ConfigurationCatalog` — static, code-owned catalog of registered keys
    (`ConfigurationDefinition`: key, type, localized display name/
    description, default, sensitivity, restart flag, numeric range). This
    is the "only explicitly registered keys are editable" boundary — the
    UI/API can never create a key.
  - `ConfigurationValue` entity — stores only the *override* per key
    (value, updated-by, updated-at). No row means "using the default";
    defaults vs. overrides stay explicit (`HasValue`).
  - `ConfigurationService` — list (merges catalog + overrides) and update
    (validates by declared type/range before any write, upserts,
    audits). Invalid values are rejected before the row is touched, so no
    partially-applied state.
  - Sensitive keys: `Value` is never included in the list/update response,
    audit metadata omits the raw value.
  - `SystemConfigurationModule` — `GET /api/v1/system-configuration`
    (`permission:configuration.view`), `PUT
    /api/v1/system-configuration/{key}` (`permission:configuration.manage`).
  - `RoleManagement.Permissions`/`PermissionPolicies` gain
    `configuration.view`/`configuration.manage`, registered as
    authorization policies in `RoleManagementModule` (required — a policy
    with no `AddPolicy` 500s instead of 401ing, caught during
    verification) and seeded via an `InsertData` migration
    (`AddConfigurationPermissions`), mirroring `AddDepartmentPermissions`.
  - Demo catalog: `general.company_display_name` (string),
    `tickets.default_page_size` (number, ranged),
    `notifications.email_enabled` (boolean),
    `integrations.smtp_password` (sensitive, requires restart) — enough to
    exercise every AC without over-building a config platform.

## Frontend

- `system-configuration/` feature: `SystemConfigurationService` (list/
  update), `SystemConfigurationList` (single table, inline per-row edit —
  no separate create/edit routes since keys aren't user-created), route
  gated by `configuration.view`, edit actions gated by
  `configuration.manage`, nav entry gated by `configuration.view`.
- Sensitive keys render a "Set/Not set" tag, never the value.

## Verification

- `SystemConfigurationTests` (persistence integration): catalog defaults,
  non-sensitive update + audit metadata, sensitive update never returned/
  audited raw, invalid value per type/range rejected with no override row
  left behind, unknown key → NotFound.
- `SystemConfigurationEndpointsAuthorizationTests` (API): anonymous → 401
  for list/update.
- `SystemConfigurationList` frontend spec: renders rows, hides sensitive
  raw value, save flow, permission-gated edit actions.
- Backend + frontend CI formatting gates.
