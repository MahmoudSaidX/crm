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
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Persistence;

namespace SquadCrm.Modules.DepartmentManagement;

public sealed class DepartmentManagementModule : IModule
{
    // The "departments.view"/"departments.manage" policies are centrally
    // owned and registered by RoleManagementModule (the permission catalog's
    // single home), following the same precedent as AuditModule's
    // AuditViewPolicy: referenced here by string only, no project reference
    // to RoleManagement.
    public string Name => "DepartmentManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DepartmentManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    DepartmentManagementSchema.MigrationsHistoryTable,
                    DepartmentManagementSchema.Name)));
        services.AddScoped<DepartmentService>();
        services.AddScoped<IDepartmentActiveLookup, DepartmentActiveLookup>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // DI resolves that same registration. No duplicate registration and
        // no project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder departments = endpoints.MapGroup("/api/v1/departments").WithTags("Departments");

        departments.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateDepartmentRequest>()
            .RequireAuthorization(PermissionPolicies.DepartmentsManage);
        departments.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.DepartmentsView);
        departments.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.DepartmentsView);
        departments.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateDepartmentRequest>()
            .RequireAuthorization(PermissionPolicies.DepartmentsManage);
        departments.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(PermissionPolicies.DepartmentsManage);
        departments.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(PermissionPolicies.DepartmentsManage);
    }

    private static async Task<IResult> CreateAsync(
        CreateDepartmentRequest request,
        DepartmentService departmentService,
        CancellationToken cancellationToken)
    {
        DepartmentMutationResult result = await departmentService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            DepartmentMutationFailure.None => Results.Created(
                $"/api/v1/departments/{result.Department!.Id}", ToResponse(result.Department)),
            DepartmentMutationFailure.DuplicateCode => DuplicateProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateDepartmentRequest request,
        DepartmentService departmentService,
        CancellationToken cancellationToken)
    {
        DepartmentMutationResult result = await departmentService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            DepartmentMutationFailure.None => Results.Ok(ToResponse(result.Department!)),
            DepartmentMutationFailure.NotFound => NotFoundProblem(),
            DepartmentMutationFailure.DuplicateCode => DuplicateProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id, DepartmentService departmentService, CancellationToken cancellationToken)
    {
        Persistence.Department? department = await departmentService.GetAsync(id, cancellationToken);
        return department is null ? NotFoundProblem() : Results.Ok(ToResponse(department));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        DepartmentService departmentService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.Department> page = await departmentService.ListAsync(pagination, cancellationToken);
        return Results.Ok(new PagedResult<DepartmentResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> ActivateAsync(
        Guid id, DepartmentService departmentService, CancellationToken cancellationToken)
    {
        DepartmentMutationResult result = await departmentService.ActivateAsync(id, cancellationToken);
        return result.Failure == DepartmentMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Department!));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, DepartmentService departmentService, CancellationToken cancellationToken)
    {
        DepartmentMutationResult result = await departmentService.DeactivateAsync(id, cancellationToken);
        return result.Failure == DepartmentMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Department!));
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Department not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "departments.not_found" });

    private static IResult DuplicateProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A department with this code already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "departments.duplicate_code" });

    private static DepartmentResponse ToResponse(Persistence.Department department) => new(
        department.Id,
        department.Code,
        department.ArabicName,
        department.EnglishName,
        department.Description,
        department.IsActive,
        department.CreatedAtUtc,
        department.UpdatedAtUtc);
}
