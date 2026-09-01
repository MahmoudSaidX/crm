# Plan — CRM-119 Manage Branches

Straightforward CRUD story. Mirror the CRM-118 `DepartmentManagement` module
pattern exactly (same schema-per-module shape, same endpoint/service/DI
conventions, same `IAuditRecorder` audit usage), substituting
Department→Branch.

## Backend — new module `BranchManagement`
- `src/backend/src/Modules/BranchManagement/SquadCrm.Modules.BranchManagement/`
  - `Persistence/Branch.cs` — Id, Code, NormalizedCode, ArabicName, EnglishName, Description?, IsActive, CreatedAtUtc, UpdatedAtUtc.
  - `Persistence/BranchManagementSchema.cs` — schema `branch_management`.
  - `Persistence/BranchManagementDbContext.cs` + `BranchManagementDbContextFactory.cs`.
  - `BranchContracts.cs` — Create/Update request + response records.
  - `BranchService.cs` — create/update/get/list/activate/deactivate, duplicate-code precheck + Postgres 23505 race handling (mirror `DepartmentService`), calls `IAuditRecorder.RecordAsync` per mutation (entity type `"Branch"`).
  - `Permissions.cs` — `branches.view` / `branches.manage` consts + internal `PermissionPolicies`.
  - `BranchManagementModule.cs` — DbContext registration, policies, `/api/v1/branches` endpoint group (POST, GET list, GET by id, PUT, POST activate, POST deactivate).
  - EF migration `InitialBranchManagement`.
  - csproj referencing BuildingBlocks, Infrastructure.Postgres, Audit.Contracts.
- Register in `SquadCrm.sln` and `Program.cs` module array.
- Permission catalog: seed `branches.view`/`branches.manage` rows in RoleManagement (new migration `AddBranchPermissions`), same cross-module precedent as `AddDepartmentPermissions`. Also add the two constants to RoleManagement's `Permissions.cs` canonical copy.

## Frontend — new feature `branches`
- `src/frontend/projects/agent-crm/src/app/branches/`
  - `branches.service.ts` (mirror `departments.service.ts`).
  - `branch-list.ts/html/scss` + spec (mirror `department-list.*`).
  - `branch-form.ts/html/scss` + spec (mirror `department-form.*`; fields Arabic name / English name / code / description).
  - `branch-translations.ts` (mirror `department-translations.ts`).
- Routes in `app.routes.ts`: `/branches`, `/branches/new`, `/branches/:id/edit`, guarded by `requirePermission('branches.view'|'branches.manage')`.
- Nav entry in `agent-shell.ts` gated on `authorization.state.has('branches.view')`; translation keys `agent.navigation.branches` in `agent-translations.ts`.
- Register `BRANCH_TRANSLATIONS` in `app.config.ts`.

## Tests
- Backend: `SquadCrm.Api.Tests/BranchEndpointsAuthorizationTests.cs` (mirror `DepartmentEndpointsAuthorizationTests.cs` — anonymous 401 coverage).
- Backend: `SquadCrm.Persistence.IntegrationTests/BranchManagementTests.cs` (mirror `DepartmentManagementTests.cs` — CRUD + duplicate-code + activate/deactivate + audit assertions against the real schema).
- Frontend: `branch-list.spec.ts`, `branch-form.spec.ts` (mirror Department specs).

## Verification
- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests, ArchitectureTests for module-boundary rules).
- `dotnet ef migrations` applied cleanly against local Postgres.
- Frontend `ng test` for the new specs, `ng build`/lint.
- Browser smoke: create/list/edit/activate/deactivate a branch in both English and Arabic (RTL) after backend is running.
