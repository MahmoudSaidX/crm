# Plan — CRM-120 Configure Custom Branding

## Backend — new module `BrandingManagement`

Schema `branding_management`. Two tables:

- `branding_setting` — singleton row (well-known fixed Id, lazily created on
  first read/write): OrganizationDisplayNameAr/En, ProductDisplayNameAr/En,
  ThemeTokensJson (small allow-listed `Dictionary<string,string>` serialized
  as JSON — keys restricted to `primaryColor`, `accentColor`), UpdatedAtUtc,
  UpdatedByHandle.
- `branding_asset` — one row per logo kind (`Primary`, `Compact`, `Favicon`),
  keyed by Kind: StorageKey, ContentType, OriginalFileName, SizeBytes,
  CreatedAtUtc, CreatedBy. Replacing a kind deletes the old file from
  `IFileStorage` after the new row commits (storage retention rule from BR).

Endpoints (`/api/v1/branding`):

- `GET /api/v1/branding` (branding.view) — full admin settings + asset
  metadata.
- `PUT /api/v1/branding` (branding.manage) — update display names + theme
  tokens (rejects non-allow-listed keys as 422).
- `POST /api/v1/branding/logo/{kind}` (branding.manage, multipart
  `IFormFile`) — validates via `IFileUploadValidator`, uploads via
  `IFileStorage`, replaces asset row, deletes prior file.
- `DELETE /api/v1/branding/logo/{kind}` (branding.manage) — removes asset +
  storage file.
- `GET /api/v1/branding/logo/{kind}` (AllowAnonymous) — streams current
  logo bytes (404 if unset). Anonymous because it must render on the
  pre-login shell.
- `GET /api/v1/branding/effective` (AllowAnonymous) — display names, logo
  URLs (only for set kinds), theme tokens; falls back to safe hardcoded
  defaults ("Squad CRM") if the row is missing/unreadable — never throws.

Permissions: `branding.view` / `branding.manage`, added to
`RoleManagement.Permissions`/`PermissionPolicies`, seeded via an EF
migration (`InsertData` on `permission_definition`, same shape as
`AddBranchPermissions`), and registered as policies in
`RoleManagementModule`. Module registered in `Program.cs` module list.

Audit: `IAuditRecorder` on every settings update and logo
upload/replace/delete (`ResourceType = "Branding"`), same pattern as
`DepartmentService.RecordAuditAsync`.

## Frontend

- `platform` lib: new `BrandingService` (parallel to `LocalizationService`)
  loading `/api/v1/branding/effective` once at app start, exposing signals
  for org/product display name and logo URLs, with the same safe defaults
  as the backend so the shell never breaks.
- Agent CRM: admin page `src/app/branding/branding-settings.{ts,html,scss}`
  (mirrors `system-configuration` list/edit pattern) behind
  `branding.view`/`branding.manage`, route `/branding`, nav item in
  `agent-shell.ts` gated on `branding.view`. File inputs for the three logo
  kinds.
- Both shells (`agent-shell`, `portal-shell`): consume `BrandingService` to
  show product display name/logo instead of (or alongside) the static
  shell title, and set `document.title`.

## Verification

- Backend: `BrandingManagementTests` (persistence/integration, mirrors
  `BranchManagementTests`) + `BrandingEndpointsAuthorizationTests` (mirrors
  `BranchEndpointsAuthorizationTests`) covering view/manage policy gating
  and the anonymous effective/logo endpoints.
- Frontend: `branding-settings.spec.ts` mirroring `branch-form.spec.ts`.
- `dotnet build`, `dotnet test` (affected projects), Angular lint/build for
  touched projects, EF migration `dotnet ef migrations add` for both new
  RoleManagement permission migration and BrandingManagement initial
  migration.
