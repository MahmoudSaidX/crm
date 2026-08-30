using Microsoft.AspNetCore.Builder;
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

        // ICurrentUserAccessor is already registered by StaffIdentityModule
        // (RegisterServices order in Program.cs runs StaffIdentity first); DI
        // resolves that same registration. No duplicate registration and no
        // project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder roles = endpoints.MapGroup("/api/v1/roles").WithTags("Roles").RequireAuthorization();

        roles.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateRoleRequest>();
        roles.MapGet("", ListAsync);
        roles.MapGet("/{id:guid}", GetAsync);
        roles.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateRoleRequest>();
        roles.MapPost("/{id:guid}/activate", ActivateAsync);
        roles.MapPost("/{id:guid}/deactivate", DeactivateAsync);
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
