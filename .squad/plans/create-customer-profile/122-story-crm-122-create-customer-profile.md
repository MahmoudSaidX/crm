# Plan — CRM-122 Create Customer Profile

Create-only story. New module `CustomerManagement`, mirroring the
`BranchManagement`/`DepartmentManagement` schema-per-module shape (same
service/endpoint/DI/audit conventions), scoped to Create only — no
edit/list/view/deactivate here (later stories).

## Backend — new module `CustomerManagement`
- `src/backend/src/Modules/CustomerManagement/SquadCrm.Modules.CustomerManagement/`
  - `Persistence/Customer.cs` — Id, CustomerNumber, FirstName, LastName, NormalizedFirstName, NormalizedLastName, PreferredLanguage?, DepartmentId?, BranchId?, Status (Active default), CreatedAtUtc, UpdatedAtUtc.
  - `Persistence/CustomerManagementSchema.cs` — schema `customer_management`.
  - `Persistence/CustomerManagementDbContext.cs` + `CustomerManagementDbContextFactory.cs`.
  - `CustomerContracts.cs` — `CreateCustomerRequest`/`CustomerResponse`.
  - `CustomerService.cs` — create only: generates `CustomerNumber` (sequential/GUID-derived, immutable), duplicate precheck on FirstName+LastName+DepartmentId+BranchId (normalized) + Postgres 23505 race handling (mirror `BranchService`), validates DepartmentId/BranchId are active via new lookup contracts (below) when provided, calls `IAuditRecorder.RecordAsync` (entity type `"Customer"`, action `"created"`).
  - `Permissions.cs` — `customers.manage` const + internal `PermissionPolicies`.
  - `CustomerManagementModule.cs` — DbContext registration, policy, `/api/v1/customers` POST endpoint only.
  - EF migration `InitialCustomerManagement`.
  - csproj referencing BuildingBlocks, Infrastructure.Postgres, Audit.Contracts, DepartmentManagement.Contracts, BranchManagement.Contracts.
- Register in `SquadCrm.sln` and `Program.cs` module array.
- Permission catalog: seed `customers.manage` in RoleManagement (new migration `AddCustomerPermissions`), same precedent as `AddBranchPermissions`.

## Backend — extract lookup contracts (architecture rule: no cross-module private-project access)
- `SquadCrm.Modules.DepartmentManagement.Contracts` (new project): `IDepartmentActiveLookup.IsActiveAsync(Guid id, CancellationToken)`, mirrors `Audit.Contracts` minimal shape.
- `SquadCrm.Modules.BranchManagement.Contracts` (new project): `IBranchActiveLookup.IsActiveAsync(Guid id, CancellationToken)`.
- Implement both interfaces inside the existing `DepartmentManagement`/`BranchManagement` module projects; register in each module's own DI extension alongside existing registrations.
- `CustomerManagement` references only these two `.Contracts` projects, never the modules' main/EF projects.

## Frontend — new feature `customers` (create only)
- `src/frontend/projects/agent-crm/src/app/customers/`
  - `customers.service.ts` (mirror `branches.service.ts`, create method only).
  - `customer-form.ts/html/scss` + spec (mirror `branch-form.*`; fields first/last name, preferred language, department/branch pickers sourced from existing active department/branch lists).
  - `customer-translations.ts` (mirror `branch-translations.ts`).
- Route `/customers/new` in `app.routes.ts`, guarded by `requirePermission('customers.manage')`.
- Nav entry in `agent-shell.ts` gated on `authorization.state.has('customers.manage')`; translation key `agent.navigation.customers` in `agent-translations.ts`.
- Register `CUSTOMER_TRANSLATIONS` in `app.config.ts`.

## Tests
- Backend: `SquadCrm.Api.Tests/CustomerEndpointsAuthorizationTests.cs` (mirror Branch equivalent — anonymous 401).
- Backend: `SquadCrm.Persistence.IntegrationTests/CustomerManagementTests.cs` (create + duplicate-detection + inactive-department/branch rejection + audit assertions).
- Frontend: `customer-form.spec.ts` (mirror `branch-form.spec.ts`).

## Verification
- `dotnet build` + `dotnet test` (Api.Tests, Persistence.IntegrationTests, ArchitectureTests for module-boundary rules — confirms CustomerManagement never references Department/Branch main projects).
- `dotnet ef migrations` applied cleanly against local Postgres; strip UTF-8 BOM from generated migration files before commit (known `dotnet format` CI issue, see commit 20bd110).
- Frontend `ng test` for new specs, `ng build`/lint.
- Browser smoke: create a customer in both English and Arabic (RTL), verify duplicate rejection and inactive department/branch rejection.
