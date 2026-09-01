# Plan — CRM-118 Manage Departments

Straightforward CRUD story. Mirror the CRM-112 Manage Roles module pattern
exactly (same schema-per-module shape, same endpoint/service/DI conventions),
substituting Role's single `Name` for `ArabicName`/`EnglishName`, and using
the CRM-114 `IAuditRecorder` for audit (not the older in-module
`RoleAuditEvent` table Roles used before CRM-114 existed).

## Backend — new module `DepartmentManagement`
- `src/backend/src/Modules/DepartmentManagement/SquadCrm.Modules.DepartmentManagement/`
  - `Persistence/Department.cs` — Id, Code, NormalizedCode, ArabicName, EnglishName, Description?, IsActive, CreatedAtUtc, UpdatedAtUtc.
  - `Persistence/DepartmentManagementSchema.cs` — schema `department_management`.
  - `Persistence/DepartmentManagementDbContext.cs` + `DepartmentManagementDbContextFactory.cs`.
  - `DepartmentContracts.cs` — Create/Update request + response records.
  - `DepartmentService.cs` — create/update/get/list/activate/deactivate, duplicate-code precheck + Postgres 23505 race handling (mirror `RoleService`), calls `IAuditRecorder.RecordAsync` per mutation.
  - `Permissions.cs` — `departments.view` / `departments.manage` consts + internal `PermissionPolicies`.
  - `DepartmentManagementModule.cs` — DbContext registration, policies, `/api/v1/departments` endpoint group (POST, GET list, GET by id, PUT, POST activate, POST deactivate).
  - EF migration `InitialDepartmentManagement` (also seeds the two new `PermissionDefinition` rows into `role_management.permission_definition` via a data migration in RoleManagement — see note below).
  - csproj referencing BuildingBlocks, Infrastructure.Postgres, Audit.Contracts.
- Register in `SquadCrm.sln` and `Program.cs` module array.
- Permission catalog: the `PermissionDefinition` table lives in the RoleManagement schema and is seeded via `RoleManagementDbContext.OnModelCreating` `HasData`. Add `departments.view`/`departments.manage` rows there (new RoleManagement migration `AddDepartmentPermissions`), same as how `audit.view` was added in `AddAuditViewPermission`. Department module's own `Permissions.cs` still owns the string constants; RoleManagement seeds the catalog rows, matching the existing cross-module precedent.

## Frontend — new feature `departments`
- `src/frontend/projects/agent-crm/src/app/departments/`
  - `departments.service.ts` (mirror `roles.service.ts`: list/get/create/update/activate/deactivate).
  - `department-list.ts/html/scss` + spec (mirror `role-list.*`).
  - `department-form.ts/html/scss` + spec (mirror `role-form.*`; fields Arabic name / English name / code / description).
  - `department-translations.ts` (mirror `role-translations.ts`).
- Routes in `app.routes.ts`: `/departments`, `/departments/new`, `/departments/:id/edit`, guarded by `requirePermission('departments.view'|'departments.manage')`.
- Nav entry in `agent-shell.ts` gated on `authorization.state.has('departments.view')`; translation keys `agent.navigation.departments` in `agent-translations.ts`.
- Register `DEPARTMENT_TRANSLATIONS` in `app.config.ts`.

## Tests
- Backend: `SquadCrm.Api.Tests/DepartmentEndpointsAuthorizationTests.cs` (mirror `RoleEndpointsAuthorizationTests.cs` — anonymous 401 coverage).
- Backend: `SquadCrm.Persistence.IntegrationTests/DepartmentManagementTests.cs` (mirror `RoleManagementTests.cs` — CRUD + duplicate-code + activate/deactivate against the real schema).
- Frontend: `department-list.spec.ts`, `department-form.spec.ts` (mirror Role specs).

## Verification
- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests, ArchitectureTests for module-boundary rules).
- `dotnet ef migrations` applied cleanly against local Postgres.
- Frontend `ng test` for the new specs, `ng build`/lint.
- Browser smoke: create/list/edit/activate/deactivate a department in both English and Arabic (RTL) after backend is running.
