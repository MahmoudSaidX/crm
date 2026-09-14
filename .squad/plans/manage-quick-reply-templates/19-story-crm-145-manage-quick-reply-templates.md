# Plan — CRM-145 Manage Quick Reply Templates

Straightforward catalog-CRUD story. Mirror the **DepartmentManagement** module
pattern (CRM-118) rather than AgentTaskManagement: a quick reply is an inert
catalog row with no lifecycle events and no cross-module consumer yet, so it
needs no domain events and no per-module outbox (YAGNI — DepartmentManagement
is the established precedent for exactly this shape).

Scope follows the story's own Deadline Acceptance Override; see the intake's
"Scope note" for what is deferred and why.

## Backend — new module

`QuickReplyManagement`
`src/backend/src/Modules/QuickReplyManagement/SquadCrm.Modules.QuickReplyManagement/`

- `Persistence/QuickReply.cs` — Id, Name, NormalizedName, ArabicContent?,
  EnglishContent?, Scope, OwnerUserId?, IsActive, CreatedAtUtc, UpdatedAtUtc.
  `QuickReplyScope` enum: `Global`, `Personal`.
- `Persistence/QuickReplyManagementSchema.cs` — schema `quick_reply_management`.
- `Persistence/QuickReplyManagementDbContext.cs` + `...DbContextFactory.cs`.
  Uniqueness: one unique index over `(normalized_name, scope, owner_user_id)`.
  Postgres treats NULLs as distinct in a unique index, so Global rows
  (owner NULL) need a **partial** unique index on `normalized_name` filtered to
  `scope = 'Global'`, plus a second partial unique index on
  `(normalized_name, owner_user_id)` filtered to `scope = 'Personal'`.
- `QuickReplyContracts.cs` — Create/Update requests, list query, response.
- `QuickReplyService.cs` — create/update/get/list/activate/deactivate, scope
  authorization, duplicate-name precheck + Postgres 23505 race handling
  (mirror `DepartmentService`), `IAuditRecorder.RecordAsync` per mutation.
- `Permissions.cs` — `quickreplies.view`, `quickreplies.manage` (personal),
  `quickreplies.manageglobal` + internal `PermissionPolicies`.
- `QuickReplyManagementModule.cs` — DbContext + service registration,
  `/api/v1/quick-replies` group (POST, GET list, GET by id, PUT,
  POST activate, POST deactivate).
- EF migration `InitialQuickReplyManagement`.
- csproj → BuildingBlocks, Infrastructure.Postgres, Audit.Contracts.

Wiring: `SquadCrm.sln`, `SquadCrm.Api.csproj`, `Program.cs` module array,
ArchitectureTests + Persistence.IntegrationTests csproj references,
`SquadCrmAssemblies`, `PostgresTestDatabase`. `scripts/migrate` discovers the
new module automatically (directory scan) — no edit needed.

Permission catalog: new RoleManagement migration `AddQuickReplyPermissions`
inserting the three `permission_definition` rows (same precedent as
`AddAgentTaskPermissions`), plus the constants in RoleManagement
`Permissions.cs`/`PermissionPolicies` and the demo role grants in
`RoleManagementDemoDataContributor`.

## Authorization model (the security core of this story)

Backend-enforced, never UI-only:

- **Create/Update/Activate/Deactivate of a `Global` template** requires
  `quickreplies.manageglobal`. A caller holding only `quickreplies.manage`
  gets 403.
- **Create of a `Personal` template** — `OwnerUserId` is always the resolved
  caller, never read from the request body, so a caller cannot create a
  template owned by someone else.
- **Update/Activate/Deactivate of a `Personal` template** requires the caller
  to BE the owner. Non-owner → 403, even with `quickreplies.manageglobal`
  (a global admin permission is not a licence to edit someone's drafts).
- **Scope is immutable after creation.** Allowing a Personal→Global promotion
  would be a privilege-escalation path through the update endpoint; a user who
  wants a global template creates one.
- **Read/list** returns Global templates plus the caller's own Personal
  templates only — another agent's personal drafts are never listed, and a
  direct GET of one returns 404 (not 403: existence itself is not disclosed).
- An unresolvable caller handle fails closed (empty list / rejected write).

Content is stored and returned as inert plain text — never evaluated,
interpolated or rendered as HTML — which is how the "no executable
expressions/scripts/traversal" Business Rule is satisfied without a variable
catalog.

Validation: at least one of ArabicContent/EnglishContent must be non-blank
(422 `quickreplies.content_required`); both bounded at 4000, name at 200.

## Frontend — new feature `quick-replies` (agent-crm)

`src/frontend/projects/agent-crm/src/app/quick-replies/`

- `quick-replies.service.ts` (mirror `departments.service.ts`).
- `quick-reply-list.ts/html/scss` + spec — PrimeNG `p-table` lazy list with
  name, scope tag, languages present, status tag, edit/activate/deactivate.
- `quick-reply-form.ts/html/scss` + spec — name, scope select (create only,
  disabled on edit), Arabic + English `p-textarea`, cross-field validator for
  "at least one language".
- `quick-reply-translations.ts` — full en/ar keys.
- Routes `/quick-replies`, `/quick-replies/new`, `/quick-replies/:id/edit`
  guarded by `requirePermission('quickreplies.view'|'quickreplies.manage')`.
- Nav entry in `agent-shell.ts` under the tasks/collaboration area, gated on
  `quickreplies.view`; `agent.navigation.quickReplies` translation keys.
- Register `QUICK_REPLY_TRANSLATIONS` in `app.config.ts`.

## Tests

- `SquadCrm.Api.Tests/QuickReplyEndpointsAuthorizationTests.cs` — anonymous 401
  on every route (mirror `DepartmentEndpointsAuthorizationTests`).
- `SquadCrm.Persistence.IntegrationTests/QuickReplyManagementTests.cs` — CRUD +
  audit, duplicate name per scope, at-least-one-language rejection, global
  permission enforcement, personal ownership enforcement, cross-user isolation
  in list/get, scope immutability, deactivate-never-deletes, migration
  integrity class.
- Frontend `quick-reply-list.spec.ts`, `quick-reply-form.spec.ts`.

## Verification

- `dotnet build`, targeted `dotnet test` (Api.Tests, Persistence.IntegrationTests,
  ArchitectureTests — module boundary/persistence rules are touched by a new module).
- Migrations apply cleanly to local Postgres; no pending migrations.
- Frontend `ng test` for the new specs + `ng lint`/`ng build`.
- Browser smoke: create/edit/activate/deactivate in English and Arabic (RTL).
