# CRM-120 — Configure Custom Branding

Source: Linear CRM-120 (project Squad CRM, parent CRM-109 Platform epic).
Blockers (all Done): CRM-200 File Storage, CRM-116 Localization, CRM-117
Responsive Shell, CRM-115 System Configuration.

## Scope (deadline override honored)

- One canonical branding config row: org/product display names (Ar/En),
  primary/compact/favicon logo references, a small allow-listed theme token
  set.
- Logo upload via shared `IFileStorage`/`IFileUploadValidator`.
- Applied to Agent CRM and Customer Portal shells (name + logo).
- Safe default fallback if branding is missing/invalid — never breaks the
  shell.
- Audited on every change.
- Out of scope (stretch, explicitly deferred): full theme-token engine,
  asset transformation pipeline, per-tenant branding, preview/versioning,
  advanced accessibility customization.

## Acceptance Criteria — see Linear CRM-120 (verbatim, not duplicated here).

## Pattern precedent

Mirrors `SystemConfiguration` (singleton-style canonical config, admin
view/manage permissions) and `DepartmentManagement`/`BranchManagement`
(module shape, audit-on-mutation). New module: `BrandingManagement`.
