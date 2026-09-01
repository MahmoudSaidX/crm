using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.RoleManagement.Persistence;

namespace SquadCrm.Modules.RoleManagement;

public sealed class RoleManagementModule : IModule
{
    public string Name => "RoleManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<RoleManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    RoleManagementSchema.MigrationsHistoryTable,
                    RoleManagementSchema.Name)));
        services.AddScoped<RoleService>();
        services.AddScoped<PermissionService>();
        services.AddScoped<AuthorizationBootstrapService>();
        services.AddScoped<StaffRoleAssignmentService>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(PermissionPolicies.RolesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.RolesView)));
            options.AddPolicy(PermissionPolicies.RolesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.RolesManage)));
            options.AddPolicy(PermissionPolicies.UsersView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.UsersView)));
            options.AddPolicy(PermissionPolicies.UsersManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.UsersManage)));
            options.AddPolicy(PermissionPolicies.AuditView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.AuditView)));
            options.AddPolicy(PermissionPolicies.DepartmentsView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.DepartmentsView)));
            options.AddPolicy(PermissionPolicies.DepartmentsManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.DepartmentsManage)));
            options.AddPolicy(PermissionPolicies.ConfigurationView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.ConfigurationView)));
            options.AddPolicy(PermissionPolicies.ConfigurationManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.ConfigurationManage)));
            options.AddPolicy(PermissionPolicies.BranchesView, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BranchesView)));
            options.AddPolicy(PermissionPolicies.BranchesManage, policy =>
                policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(Permissions.BranchesManage)));
        });

        // ICurrentUserAccessor is already registered by StaffIdentityModule
        // (RegisterServices order in Program.cs runs StaffIdentity first); DI
        // resolves that same registration. No duplicate registration and no
        // project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder roles = endpoints.MapGroup("/api/v1/roles").WithTags("Roles");

        roles.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateRoleRequest>()
            .RequireAuthorization(PermissionPolicies.RolesManage);
        roles.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.RolesView);
        roles.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.RolesView);
        roles.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateRoleRequest>()
            .RequireAuthorization(PermissionPolicies.RolesManage);
        roles.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(PermissionPolicies.RolesManage);
        roles.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(PermissionPolicies.RolesManage);
        roles.MapGet("/{id:guid}/permissions", GetRolePermissionsAsync)
            .RequireAuthorization(PermissionPolicies.RolesView);
        roles.MapPut("/{id:guid}/permissions", ReplaceRolePermissionsAsync)
            .ValidatesDataAnnotations<ReplaceRolePermissionsRequest>()
            .RequireAuthorization(PermissionPolicies.RolesManage);

        endpoints.MapGet("/api/v1/permissions", GetPermissionCatalogAsync)
            .WithTags("Permissions")
            .RequireAuthorization(PermissionPolicies.RolesView);
        endpoints.MapGet("/api/v1/authorization/me", GetCurrentPermissionsAsync)
            .WithTags("Authorization")
            .RequireAuthorization();

        RouteGroupBuilder staffRoles = endpoints
            .MapGroup("/api/v1/staff-users/{staffSubjectId:guid}/roles")
            .WithTags("StaffRoleAssignments");
        staffRoles.MapGet("", GetStaffRolesAsync).RequireAuthorization(PermissionPolicies.RolesView);
        staffRoles.MapPut("", ReplaceStaffRolesAsync)
            .ValidatesDataAnnotations<ReplaceStaffRolesRequest>()
            .RequireAuthorization(PermissionPolicies.RolesManage);
    }

    private static async Task<IResult> GetStaffRolesAsync(
        Guid staffSubjectId,
        StaffRoleAssignmentService assignmentService,
        CancellationToken cancellationToken) =>
        Results.Ok(await assignmentService.GetAssignedRolesAsync(staffSubjectId, cancellationToken));

    private static async Task<IResult> ReplaceStaffRolesAsync(
        Guid staffSubjectId,
        ReplaceStaffRolesRequest request,
        StaffRoleAssignmentService assignmentService,
        CancellationToken cancellationToken)
    {
        StaffRoleAssignmentResult result = await assignmentService.ReplaceAsync(
            staffSubjectId, request.RoleIds, cancellationToken);
        return result.Failure switch
        {
            StaffRoleAssignmentFailure.None => Results.NoContent(),
            StaffRoleAssignmentFailure.SubjectNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Staff subject not found.",
                extensions: new Dictionary<string, object?> { ["code"] = "staff_users.not_found" }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["roleIds"] = ["Role ids must be unique, existing role identifiers."],
            }),
        };
    }

    private static async Task<IResult> GetPermissionCatalogAsync(
        PermissionService permissionService,
        CancellationToken cancellationToken) =>
        Results.Ok(await permissionService.GetCatalogAsync(roleId: null, cancellationToken));

    private static async Task<IResult> GetRolePermissionsAsync(
        Guid id,
        PermissionService permissionService,
        CancellationToken cancellationToken)
    {
        if (!await permissionService.RoleExistsAsync(id, cancellationToken))
        {
            return NotFoundProblem();
        }

        return Results.Ok(await permissionService.GetCatalogAsync(id, cancellationToken));
    }

    private static async Task<IResult> ReplaceRolePermissionsAsync(
        Guid id,
        ReplaceRolePermissionsRequest request,
        PermissionService permissionService,
        CancellationToken cancellationToken)
    {
        ReplacePermissionsResult result = await permissionService.ReplaceAsync(
            id, request.PermissionCodes, cancellationToken);
        return result.Failure switch
        {
            ReplacePermissionsFailure.None => Results.NoContent(),
            ReplacePermissionsFailure.RoleNotFound => NotFoundProblem(),
            ReplacePermissionsFailure.RoleInactive => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Permissions cannot be changed for an inactive role.",
                extensions: new Dictionary<string, object?> { ["code"] = "permissions.role_inactive" }),
            _ => Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["permissionCodes"] = ["Permission codes must be unique, non-empty catalog entries."],
            }),
        };
    }

    private static async Task<IResult> GetCurrentPermissionsAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        PermissionService permissionService,
        CancellationToken cancellationToken)
    {
        string? subject = principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(subject, out Guid staffSubjectId))
        {
            return Results.Forbid();
        }

        IReadOnlyList<string> permissionCodes = await permissionService.GetCurrentPermissionsAsync(
            staffSubjectId, cancellationToken);
        return Results.Ok(new CurrentPermissionsResponse(permissionCodes));
    }

    private static async Task<IResult> CreateAsync(
        CreateRoleRequest request,
        RoleService roleService,
        CancellationToken cancellationToken)
    {
        RoleMutationResult result = await roleService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            RoleMutationFailure.None => Results.Created(
                $"/api/v1/roles/{result.Role!.Id}", ToResponse(result.Role)),
            RoleMutationFailure.DuplicateName => DuplicateProblem("roles.duplicate_name", "name"),
            RoleMutationFailure.DuplicateCode => DuplicateProblem("roles.duplicate_code", "code"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateRoleRequest request,
        RoleService roleService,
        CancellationToken cancellationToken)
    {
        RoleMutationResult result = await roleService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            RoleMutationFailure.None => Results.Ok(ToResponse(result.Role!)),
            RoleMutationFailure.NotFound => NotFoundProblem(),
            RoleMutationFailure.DuplicateName => DuplicateProblem("roles.duplicate_name", "name"),
            RoleMutationFailure.DuplicateCode => DuplicateProblem("roles.duplicate_code", "code"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> GetAsync(Guid id, RoleService roleService, CancellationToken cancellationToken)
    {
        Persistence.Role? role = await roleService.GetAsync(id, cancellationToken);
        return role is null ? NotFoundProblem() : Results.Ok(ToResponse(role));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        RoleService roleService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.Role> page = await roleService.ListAsync(pagination, cancellationToken);
        return Results.Ok(new PagedResult<RoleResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> ActivateAsync(Guid id, RoleService roleService, CancellationToken cancellationToken)
    {
        RoleMutationResult result = await roleService.ActivateAsync(id, cancellationToken);
        return result.Failure == RoleMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Role!));
    }

    private static async Task<IResult> DeactivateAsync(Guid id, RoleService roleService, CancellationToken cancellationToken)
    {
        RoleMutationResult result = await roleService.DeactivateAsync(id, cancellationToken);
        return result.Failure == RoleMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Role!));
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Role not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "roles.not_found" });

    private static IResult DuplicateProblem(string code, string field) => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: $"A role with this {field} already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = code });

    private static RoleResponse ToResponse(Persistence.Role role) => new(
        role.Id, role.Name, role.Code, role.Description, role.IsActive, role.CreatedAtUtc, role.UpdatedAtUtc);
}
