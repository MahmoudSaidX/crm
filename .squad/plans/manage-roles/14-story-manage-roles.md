# Story 14 — Manage Roles (Story: CRM-112)

## Prerequisites

- Story 13 completed: `../user-authentication-session-management/13-story-crm-110-user-authentication-session-management.md` — establishes the `StaffIdentity` module, the schema-per-module EF Core convention, the minimal-API `IModule` pattern, `ICurrentUserAccessor`, and the `agent-crm` `auth`/`home` route/guard baseline this story reuses.
- Story 7 (`../domain-events-integration-events-transactional-outbox/07-story-crm-198-domain-events-integration-events-transactional-outbox.md`) established `HasDomainEvents`/`IDomainEvent`/`OutboxMessage` for **cross-module** durable integration. This story does **not** use that pattern — see "Audit pattern decision" below.
- CRM-111 (Manage Users) and CRM-113 (Configure Role Permissions) are Backlog and out of scope: build nothing that assigns permissions to a role or roles to a user.

---

## Story Goal

Let an authenticated staff user manage the global `Role` catalog end to end:

1. Create a role (`Name`, `Code`, optional `Description`).
2. List roles (paged) and view one role's detail.
3. Edit a role's `Name`, `Code`, `Description`.
4. Activate / deactivate a role (soft-disable; no hard delete).
5. Every create/update/activate/deactivate is recorded as an audit event.

**Not in scope** (explicitly deferred to other stories, per intake): assigning permissions to a role (CRM-113), assigning a role to a user (CRM-111), any Branch/Department scope on `Role`, any organizational-scope enforcement, any permission-based endpoint policy (none exists yet in this repo — endpoints here use bare `RequireAuthorization()`, identical to `StaffIdentityModule.MapEndpoints`'s `/me` endpoint, `SquadCrm.Modules.StaffIdentity/StaffIdentityModule.cs:102`).

---

## Audit pattern decision (report to controller, not a further choice for the executor)

Two audit patterns exist in this repo:

- **Cross-module integration events** (`HasDomainEvents` + a module-owned `SaveChangesInterceptor` + `OutboxMessage`), proven in `SquadCrm.Modules.ArchitectureFixture` (CRM-198). Built for a domain event that another module or an external consumer must durably observe.
- **Direct intra-module audit row**, used for real by `StaffIdentity`: `AuthenticationEvent` (`SquadCrm.Modules.StaffIdentity/Persistence/AuthenticationEvent.cs`) is inserted straight into the same `DbContext`/transaction as the state change (see `SquadCrm.Modules.StaffIdentity/AuthenticationService.cs:164` — `dbContext.AuthenticationEvents.Add(new AuthenticationEvent {...})` then a single `SaveChangesAsync`). No interceptor, no outbox, no event type.

Nothing outside this story currently needs to observe a role change (CRM-111/CRM-113 are unbuilt and out of scope), so there is no cross-module consumer to integrate with. Follow the `AuthenticationEvent` precedent: a plain `RoleAuditEvent` row inserted in the same `SaveChangesAsync` call as the `Role` mutation. Introducing the outbox/integration-event machinery here would be speculative (YAGNI) — nothing durable needs to leave the module yet. This mirrors the pattern already accepted for the sibling module in the same epic (CRM-108), not an invented one.

---

## Context — Read These Files First

1. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/StaffIdentityModule.cs` — the `IModule` shape to copy: `RegisterServices` (DbContext + scoped services), `MapEndpoints` (route group, `RequireAuthorization`, `ValidatesDataAnnotations<T>()`), static endpoint-handler methods returning `IResult`.
2. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/Persistence/StaffIdentityDbContext.cs` — `OnModelCreating` conventions: `HasDefaultSchema`, snake_case `ToTable`/`HasColumnName`, `HasIndex(...).IsUnique()`.
3. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/Persistence/StaffUser.cs` and `AuthenticationEvent.cs` — entity shape precedent (plain EF Core POCOs, no `HasDomainEvents`).
4. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/Persistence/StaffIdentitySchema.cs` and `StaffIdentityDbContextFactory.cs` — schema-name constant + `IDesignTimeDbContextFactory` boilerplate to copy verbatim with new names.
5. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/AuthenticationService.cs:36-59` and `:160-171` — normalization (`NormalizeEmail`, ~line 174, `email.Trim().ToUpperInvariant()`) and the audit-row-in-same-SaveChanges pattern to mirror for `RoleAuditEvent`.
6. `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/AuthenticationContracts.cs` — request/response `record` shape with `[Required]`/`[MaxLength]` data annotations.
7. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Http/PagedResult.cs` and `PaginationRequest.cs` — use these verbatim for the list endpoint (`[AsParameters] PaginationRequest`, 1-based `Page`).
8. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Errors/ProblemDetailsExtensions.cs:28` (`CodeExtensionName = "code"`) — each module owns its own error codes; use `roles.duplicate_name`, `roles.duplicate_code`, `roles.not_found` the same way `AuthenticationService`'s caller uses `authentication.invalid_credentials` (`StaffIdentityModule.cs:118`).
9. `src/backend/src/BuildingBlocks/SquadCrm.BuildingBlocks/Security/ICurrentUserAccessor.cs` and `src/backend/src/Modules/StaffIdentity/SquadCrm.Modules.StaffIdentity/HttpCurrentUserAccessor.cs` — already registered globally by `StaffIdentityModule.RegisterServices` (`services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>()`, line 47); inject `ICurrentUserAccessor` in the new module for `RoleAuditEvent.ChangedByHandle` without adding a project reference to `StaffIdentity`.
10. `src/backend/src/Api/SquadCrm.Api/Program.cs:151-157` — the explicit `IModule[] modules` array; add the new module here.
11. `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj` — add a `ProjectReference` to the new module, next to the existing `StaffIdentity` reference.
12. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresTestDatabase.cs:82-85` and `:117-118` — add a `CreateRoleManagementContext()` static factory and call `.Database.MigrateAsync()` in `InitializeAsync`, exactly like `CreateStaffIdentityContext()`.
13. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/StaffAuthenticationTests.cs:21-46` — integration test shape: real Postgres, direct `DbContext`, assert on the audit row's `EventType`/absence of sensitive data.
14. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/SchemaOwnershipTests.cs` and `MigrationTests.cs` — schema/migration test precedent (currently parameterized to `architecture_fixture`); add role-management equivalents or extend to cover the new schema.
15. `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs` — add `RoleManagement` to `All`/`ModulesNamespacePrefix`-covered list so architecture rules apply to the new module (mirrors `StaffIdentity` entries, lines 51/69).
16. `src/backend/tests/SquadCrm.Api.Tests/ModuleRegistrationTests.cs` and `AuthenticationBoundaryTests.cs` — precedent for asserting a module is registered and its endpoints require authorization.
17. `src/frontend/projects/agent-crm/src/app/app.routes.ts` — routes live under `app/<capability>/...`, no shell yet (`CRM-117` note in the file's own doc comment); add a `roles` route the same flat way `login`/`home` are added.
18. `src/frontend/projects/agent-crm/src/app/auth/auth.service.ts` and `auth.guard.ts` — service/guard conventions (signals, `inject`, `firstValueFrom`) and the guard to reuse (`canActivate: [authGuard]`) for the new routes.
19. `src/frontend/projects/agent-crm/src/app/auth/login.ts` and `login.html` — PrimeNG reactive-form component shape (`ChangeDetectionStrategy.OnPush`, `FormGroup`/`FormControl` with `Validators`, `@if` control-flow, hardcoded English copy — no i18n dictionary exists yet, `LocaleService`'s own doc comment says "CRM-116 owns content").
20. `src/frontend/package.json:25-27` — PrimeNG `^20.4.0` already installed; use `primeng/table`, `primeng/dialog` (or `primeng/drawer`), `primeng/button`, `primeng/inputtext`, `primeng/textarea`, `primeng/toggleswitch`, `primeng/tag`, `primeng/paginator`/built-in `p-table` paging — no new package needed.
21. `src/frontend/projects/platform/src/lib/http/api-base-url.interceptor.ts` — relative API URLs (e.g. `/api/v1/roles`) are auto-prefixed; no absolute URL needed in the new service.

---

## Backend Tasks

### 1 — New module skeleton

**Create directory:** `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/`

**Create file: `SquadCrm.Modules.RoleManagement.csproj`** — copy `SquadCrm.Modules.StaffIdentity.csproj` verbatim except: drop the `Microsoft.AspNetCore.Authentication.JwtBearer` package reference (not needed — this module adds no auth scheme), keep `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design`, keep the `SquadCrm.BuildingBlocks` and `SquadCrm.Infrastructure.Postgres` `ProjectReference`s.

**Create file: `Properties/AssemblyInfo.cs`** — copy `SquadCrm.Modules.StaffIdentity/Properties/AssemblyInfo.cs` verbatim (adjust namespace if it names the assembly).

**Add to solution:** `dotnet sln src/backend/SquadCrm.sln add src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/SquadCrm.Modules.RoleManagement.csproj` (creates a top-level entry; move it under a `RoleManagement` solution folder the same way `StaffIdentity`/`ArchitectureFixture` are nested, matching `SquadCrm.sln:44-48`).

### 2 — Persistence

**Create file: `Persistence/RoleManagementSchema.cs`** — copy `StaffIdentitySchema.cs`, `Name = "role_management"`.

**Create file: `Persistence/Role.cs`**

```csharp
namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

**Create file: `Persistence/RoleAuditEvent.cs`**

```csharp
namespace SquadCrm.Modules.RoleManagement.Persistence;

public sealed class RoleAuditEvent
{
    public long Id { get; set; }
    public Guid RoleId { get; set; }
    public required string EventType { get; set; }
    public string? ChangedByHandle { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
```

`EventType` values: `"created"`, `"updated"`, `"activated"`, `"deactivated"` — no `Outcome` field (unlike `AuthenticationEvent`): every role-admin action here is a transactional CRUD write with no partial-failure outcome to record (validation failures never reach the audit table at all); do not add one speculatively.

**Create file: `Persistence/RoleManagementDbContext.cs`** — copy `StaffIdentityDbContext.cs` shape:

```csharp
public sealed class RoleManagementDbContext(DbContextOptions<RoleManagementDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RoleAuditEvent> RoleAuditEvents => Set<RoleAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RoleManagementSchema.Name);

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("role");
            entity.HasKey(role => role.Id);
            entity.Property(role => role.Name).HasColumnName("name").HasMaxLength(200);
            entity.Property(role => role.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
            entity.HasIndex(role => role.NormalizedName).IsUnique();
            entity.Property(role => role.Code).HasColumnName("code").HasMaxLength(64);
            entity.Property(role => role.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(64);
            entity.HasIndex(role => role.NormalizedCode).IsUnique();
            entity.Property(role => role.Description).HasColumnName("description").HasMaxLength(1000);
            entity.Property(role => role.IsActive).HasColumnName("is_active");
            entity.Property(role => role.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(role => role.UpdatedAtUtc).HasColumnName("updated_at_utc");
        });

        modelBuilder.Entity<RoleAuditEvent>(entity =>
        {
            entity.ToTable("role_audit_event");
            entity.HasKey(auditEvent => auditEvent.Id);
            entity.Property(auditEvent => auditEvent.RoleId).HasColumnName("role_id");
            entity.Property(auditEvent => auditEvent.EventType).HasColumnName("event_type").HasMaxLength(32);
            entity.Property(auditEvent => auditEvent.ChangedByHandle).HasColumnName("changed_by_handle").HasMaxLength(256);
            entity.Property(auditEvent => auditEvent.OccurredAtUtc).HasColumnName("occurred_at_utc");
        });
    }
}
```

**Create file: `Persistence/RoleManagementDbContextFactory.cs`** — copy `StaffIdentityDbContextFactory.cs` verbatim, substituting the new context/schema types.

**Generate migration:** from `src/backend/src/Modules/RoleManagement/SquadCrm.Modules.RoleManagement/`, run `dotnet ef migrations add InitialRoleManagement --context RoleManagementDbContext -o Persistence/Migrations` (same tool invocation pattern the `StaffIdentity` migration under `Persistence/Migrations/20260829143339_InitialStaffIdentity.cs` used).

### 3 — Normalization + service

**Create file: `RoleContracts.cs`**

```csharp
using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.RoleManagement;

public sealed record CreateRoleRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(64)] string Code,
    [property: MaxLength(1000)] string? Description);

public sealed record UpdateRoleRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: Required, MaxLength(64)] string Code,
    [property: MaxLength(1000)] string? Description);

public sealed record RoleResponse(
    Guid Id, string Name, string Code, string? Description, bool IsActive,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
```

**Create file: `RoleService.cs`** — scoped service, constructor-injects `RoleManagementDbContext` and `ICurrentUserAccessor`. Methods: `CreateAsync`, `UpdateAsync`, `GetAsync`, `ListAsync(PaginationRequest)`, `ActivateAsync`, `DeactivateAsync`, all `async Task<...>` taking a `CancellationToken`. Normalize with `internal static string Normalize(string value) => value.Trim().ToUpperInvariant();` (mirrors `AuthenticationService.NormalizeEmail`, `AuthenticationService.cs:174`). Before insert/update, query `Roles.AnyAsync(r => r.NormalizedName == normalizedName && r.Id != excludedId)` and the same for `NormalizedCode`; return a sentinel (e.g. a small result enum/discriminated result, matching `AuthenticationResult?`-style nullable-return precedent in `AuthenticationService`) so the endpoint can map a duplicate to `roles.duplicate_name` / `roles.duplicate_code` the same way `StaffIdentityModule.SignInAsync` maps a `null` result to `authentication.invalid_credentials` (`StaffIdentityModule.cs:116-119`). Every mutating method inserts one `RoleAuditEvent` row (`ChangedByHandle = currentUserAccessor.Handle`) and calls `SaveChangesAsync` once (same transaction as the `Role` write) — mirror `AuthenticationService.cs:164-171`.

### 4 — Module + endpoints

**Create file: `RoleManagementModule.cs`** — `IModule` implementation, `Name => "RoleManagement"`.

`RegisterServices`: `services.AddDbContext<RoleManagementDbContext>(...)` (same `UseNpgsql` + `MigrationsHistoryTable` pattern as `StaffIdentityModule.cs:40-44`), `services.AddScoped<RoleService>()`. Do **not** register `ICurrentUserAccessor` here — it is already registered by `StaffIdentityModule` and DI resolves the same singleton registration; do not add a duplicate registration or a project reference to `StaffIdentity`.

`MapEndpoints`:

```csharp
RouteGroupBuilder roles = endpoints.MapGroup("/api/v1/roles").WithTags("Roles").RequireAuthorization();

roles.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateRoleRequest>();
roles.MapGet("", ListAsync);
roles.MapGet("/{id:guid}", GetAsync);
roles.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateRoleRequest>();
roles.MapPost("/{id:guid}/activate", ActivateAsync);
roles.MapPost("/{id:guid}/deactivate", DeactivateAsync);
```

`ListAsync` takes `[AsParameters] PaginationRequest pagination` and returns `PagedResult<RoleResponse>` (see `PagedResult.cs`/`PaginationRequest.cs`, Context item 7). `GetAsync`/`UpdateAsync`/`ActivateAsync`/`DeactivateAsync` return `Results.NotFound()` with a `roles.not_found` problem extension when the id does not exist (mirror the `Results.Problem(statusCode:..., extensions: new Dictionary<string,object?> { ["code"] = "..." })` shape at `StaffIdentityModule.cs:118`). `CreateAsync`/`UpdateAsync` map a duplicate-name/duplicate-code result to `Results.Problem(statusCode: StatusCodes.Status409Conflict, ...)` with `roles.duplicate_name` / `roles.duplicate_code`.

### 5 — Wire the module in

**File: `src/backend/src/Api/SquadCrm.Api/Program.cs`** — add `new SquadCrm.Modules.RoleManagement.RoleManagementModule(),` to the `IModule[] modules` array (Program.cs:153, right after the `StaffIdentity` entry).

**File: `src/backend/src/Api/SquadCrm.Api/SquadCrm.Api.csproj`** — add `<ProjectReference Include="../../Modules/RoleManagement/SquadCrm.Modules.RoleManagement/SquadCrm.Modules.RoleManagement.csproj" />` next to the `StaffIdentity` reference.

### 6 — Test wiring

**File: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/PostgresTestDatabase.cs`** — add `using SquadCrm.Modules.RoleManagement.Persistence;`, add `public static RoleManagementDbContext CreateRoleManagementContext() => new RoleManagementDbContextFactory().CreateDbContext([]);` (next to `CreateStaffIdentityContext`, line 117-118), and in `InitializeAsync` (after line 85) add `await using RoleManagementDbContext roleManagement = CreateRoleManagementContext(); await roleManagement.Database.MigrateAsync();`.

**File: `src/backend/tests/SquadCrm.Persistence.IntegrationTests/SquadCrm.Persistence.IntegrationTests.csproj`** — add a `ProjectReference` to the new module project, next to the `StaffIdentity` one.

**File: `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrmAssemblies.cs`** — add `using SquadCrm.Modules.RoleManagement;`, `public static Assembly RoleManagement { get; } = typeof(RoleManagementModule).Assembly;`, add `RoleManagement` to the `All` list (mirrors `StaffIdentity`, lines 51/69).

**File: `src/backend/tests/SquadCrm.ArchitectureTests/SquadCrm.ArchitectureTests.csproj`** and **`src/backend/tests/SquadCrm.Api.Tests/*.csproj`** — add a `ProjectReference` to the new module project wherever the `StaffIdentity` project is already referenced.

---

## Frontend Tasks

### 7 — Roles API service

**Create file: `src/frontend/projects/agent-crm/src/app/roles/roles.service.ts`** — `@Injectable({ providedIn: 'root' })`, mirrors `auth.service.ts`'s `inject(HttpClient)` + `firstValueFrom` style. Methods: `list(page, pageSize)` → `GET /api/v1/roles`, `get(id)` → `GET /api/v1/roles/:id`, `create(request)` → `POST /api/v1/roles`, `update(id, request)` → `PUT /api/v1/roles/:id`, `activate(id)` / `deactivate(id)` → the two `POST` actions. Define local `interface Role { id: string; name: string; code: string; description: string | null; isActive: boolean; createdAtUtc: string; updatedAtUtc: string }` and a `PagedResult<T>` matching the backend's `PagedResult<TItem>` shape (`items`, `page`, `pageSize`, `totalCount` — confirm exact casing against the backend's JSON serializer defaults, which are camelCase per `AccessCredentialResponse`'s `accessToken`/`expiresAt` wire shape in `auth.service.ts:6-9`).

### 8 — Roles screens

**Create file: `src/frontend/projects/agent-crm/src/app/roles/role-list.ts`** (+ `.html`, `.scss`) — `ChangeDetectionStrategy.OnPush`, `p-table` bound to a `signal<Role[]>`, columns Name/Code/Description/Status (use `p-tag` with `severity="success"`/`"danger"` for active/inactive), row actions: Edit (routerLink to `role-form` with id), Activate/Deactivate (calls the service method for the row, then refetches the current page). A "New role" `p-button` navigates to the create route. Paginate with `p-table`'s built-in `[rows]`/`[totalRecords]`/`(onPage)` lazy-loading, calling `RolesService.list`.

**Create file: `src/frontend/projects/agent-crm/src/app/roles/role-form.ts`** (+ `.html`, `.scss`) — one component for both create and edit (edit when the route has an `:id` param), `FormGroup` with `name`/`code`/`description` `FormControl`s and `Validators.required`/`maxLength` matching the backend's `[MaxLength]` bounds (200/64/1000), same submit/error pattern as `login.ts:42-58` (`submitting` signal, try/catch, surface a `p-message` on a 409 duplicate response). On successful submit, navigate back to the list route.

### 9 — Routes

**File: `src/frontend/projects/agent-crm/src/app/app.routes.ts`** — add, guarded the same way `home` is guarded:

```typescript
{
  path: 'roles',
  canActivate: [authGuard],
  loadComponent: () => import('./roles/role-list').then((m) => m.RoleList),
},
{
  path: 'roles/new',
  canActivate: [authGuard],
  loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
},
{
  path: 'roles/:id/edit',
  canActivate: [authGuard],
  loadComponent: () => import('./roles/role-form').then((m) => m.RoleForm),
},
```

Place these before the `'**'` catch-all (`app.routes.ts`'s last entry). Do **not** add a navigation/shell entry point (menu, header link) — `CRM-117` owns the application shell; these routes are reachable directly (as `home`/`login` already are) until CRM-117 links them in.

---

## Edge Cases & Failure Modes

- **Duplicate name/code differing only in case or surrounding whitespace** (e.g. `" Sales Manager"` vs `"Sales Manager"`) — must collide, enforced by `NormalizedName`/`NormalizedCode` unique indexes and the pre-check in `RoleService`, mirroring `AuthenticationService.NormalizeEmail` (`AuthenticationService.cs:174`).
- **Race between two concurrent creates with the same name/code** — the pre-check can both pass before either insert commits; the unique index is the real guard. `RoleService.CreateAsync` must catch the resulting `DbUpdateException` (Postgres unique-violation, SQLSTATE `23505`) and translate it to the same duplicate result the pre-check produces, not let a 500 leak through. Test this explicitly (see Test Plan #4).
- **Deactivating a role that is later referenced** (by CRM-111 role-assignment, not built yet) — this story enforces nothing about references (out of scope), but `DeactivateAsync` must succeed unconditionally for any existing, active role; it never deletes the row.
- **Unknown id on Get/Update/Activate/Deactivate** — return `404` with `roles.not_found`, never a 500 or an empty-body 200.
- **Empty `Description`** — must be accepted as `null`/empty; it is optional per the Fields Dictionary.
- **`ICurrentUserAccessor.Handle` is `null`** (e.g. claim-mapping edge case already present in `HttpCurrentUserAccessor`, unrelated to this story) — `RoleAuditEvent.ChangedByHandle` must tolerate `null` (column is nullable); do not throw.
- **Arabic text in `Name`/`Description`** — `nvarchar`/Postgres `text`/`varchar` columns are UTF-8 by default in this stack (see `StaffUser.NormalizedEmail` precedent); no special-casing needed, but do not silently truncate multi-byte input at a byte boundary — `HasMaxLength` here is character-length, which EF/Npgsql already enforces correctly.

---

## Test Plan

1. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/RoleManagementTests.cs` (new) — real-Postgres tests via `PostgresTestDatabase`: create succeeds and produces one `"created"` `RoleAuditEvent`; update succeeds and produces `"updated"`; activate/deactivate each produce their event type; duplicate name (same/different case, with whitespace) is rejected pre-insert; duplicate code likewise; concurrent duplicate insert is rejected via the unique-index catch path (Edge Case #2).
2. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/SchemaOwnershipTests.cs` — extend or add a `role_management`-schema variant asserting the schema exists, `role`/`role_audit_event` tables exist in it, and columns are snake_case (mirror the existing `architecture_fixture` assertions).
3. `src/backend/tests/SquadCrm.Persistence.IntegrationTests/MigrationTests.cs` — extend or add: `InitialRoleManagement` applies cleanly to a fresh database and no pending migrations remain (mirror the `ArchitectureFixtureDbContext` test shape).
4. `src/backend/tests/SquadCrm.Api.Tests/AuthenticationBoundaryTests.cs` or a new `RoleEndpointsAuthorizationTests.cs` — every `/api/v1/roles*` route returns `401` unauthenticated (mirror the existing StaffIdentity boundary assertions).
5. `src/backend/tests/SquadCrm.Api.Tests/ModuleRegistrationTests.cs` — extend to assert `RoleManagementModule` is present in the registered module set.
6. `src/backend/tests/SquadCrm.ArchitectureTests/ArchitectureRulesTests.cs` — no new rule needed if `SquadCrmAssemblies.All` is updated (Task 6); confirm the existing module-boundary rules (`ModuleContracts_MustNotDependOnModuleImplementationOrApi`, schema-isolation rules) pass unmodified against the new module.
7. `src/frontend/projects/agent-crm/src/app/roles/role-list.spec.ts` (new) — list renders rows from a mocked `RolesService`, activate/deactivate call the right endpoint and refresh the row.
8. `src/frontend/projects/agent-crm/src/app/roles/role-form.spec.ts` (new) — required-field validation blocks submit; a mocked 409 response surfaces the duplicate error message; successful submit navigates to the list.
9. `src/frontend/projects/agent-crm/src/app/roles/roles.service.spec.ts` (new) — each method issues the expected method/URL/body against `HttpTestingController` (mirror any existing `auth.service.spec.ts` if present; otherwise follow Angular's standard `HttpClientTestingModule` pattern).

---

## Migration / Rollback

- New migration `InitialRoleManagement` only adds the new `role_management` schema and its two tables — no existing schema or table is touched, so rollback is `dotnet ef database update 0 --context RoleManagementDbContext` (drops only this module's schema) if the story must be reverted before any dependent story lands.
- No data backfill: this is a brand-new module with no pre-existing rows.

---

## Verification Steps

1. **Backend builds:** `dotnet build src/backend/SquadCrm.sln` — no errors.
2. **Backend tests pass:** `dotnet test src/backend/SquadCrm.sln` — all new and existing tests green (requires Postgres reachable per `PostgresTestDatabase`'s own `docker compose up -d` instruction).
3. **Architecture tests pass:** `dotnet test src/backend/tests/SquadCrm.ArchitectureTests` — module-boundary rules still hold with the new module included.
4. **Frontend unit tests pass:** `ng test` (or the repo's existing frontend test command) from `src/frontend/` — new `roles` specs green.
5. **Frontend builds:** `ng build agent-crm` from `src/frontend/` — no errors.
6. **Regression:** sign in via `/login`, navigate directly to `/roles`, create a role, edit it, deactivate it, confirm the list reflects each change and no console/network error appears.

---

## Done Criteria

- [ ] Authorized (authenticated) staff can create, view, edit, list, activate and deactivate roles via `/api/v1/roles*` and the `agent-crm` `/roles` screens.
- [ ] Role name/code uniqueness is validated (case/whitespace-insensitive) both pre-insert and at the database unique-index level.
- [ ] Deactivating a role never deletes it; it remains readable/listable with `isActive: false`.
- [ ] Every create/update/activate/deactivate produces exactly one `RoleAuditEvent` row.
- [ ] No permission-assignment, user-role-assignment, or organizational-scope logic was added (CRM-111/CRM-113 remain untouched).
- [ ] `00-overview.md` updated with this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
