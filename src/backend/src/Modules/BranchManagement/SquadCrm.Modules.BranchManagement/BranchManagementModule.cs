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
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.BranchManagement.Persistence;

namespace SquadCrm.Modules.BranchManagement;

public sealed class BranchManagementModule : IModule
{
    // The "branches.view"/"branches.manage" policies are centrally
    // owned and registered by RoleManagementModule (the permission catalog's
    // single home), following the same precedent as AuditModule's
    // AuditViewPolicy: referenced here by string only, no project reference
    // to RoleManagement.
    public string Name => "BranchManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BranchManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    BranchManagementSchema.MigrationsHistoryTable,
                    BranchManagementSchema.Name)));
        services.AddScoped<BranchService>();
        services.AddScoped<IBranchActiveLookup, BranchActiveLookup>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // DI resolves that same registration. No duplicate registration and
        // no project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder branches = endpoints.MapGroup("/api/v1/branches").WithTags("Branches");

        branches.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateBranchRequest>()
            .RequireAuthorization(PermissionPolicies.BranchesManage);
        branches.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.BranchesView);
        branches.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.BranchesView);
        branches.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateBranchRequest>()
            .RequireAuthorization(PermissionPolicies.BranchesManage);
        branches.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(PermissionPolicies.BranchesManage);
        branches.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(PermissionPolicies.BranchesManage);
    }

    private static async Task<IResult> CreateAsync(
        CreateBranchRequest request,
        BranchService branchService,
        CancellationToken cancellationToken)
    {
        BranchMutationResult result = await branchService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            BranchMutationFailure.None => Results.Created(
                $"/api/v1/branches/{result.Branch!.Id}", ToResponse(result.Branch)),
            BranchMutationFailure.DuplicateCode => DuplicateProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateBranchRequest request,
        BranchService branchService,
        CancellationToken cancellationToken)
    {
        BranchMutationResult result = await branchService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            BranchMutationFailure.None => Results.Ok(ToResponse(result.Branch!)),
            BranchMutationFailure.NotFound => NotFoundProblem(),
            BranchMutationFailure.DuplicateCode => DuplicateProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id, BranchService branchService, CancellationToken cancellationToken)
    {
        Persistence.Branch? branch = await branchService.GetAsync(id, cancellationToken);
        return branch is null ? NotFoundProblem() : Results.Ok(ToResponse(branch));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        BranchService branchService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.Branch> page = await branchService.ListAsync(pagination, cancellationToken);
        return Results.Ok(new PagedResult<BranchResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> ActivateAsync(
        Guid id, BranchService branchService, CancellationToken cancellationToken)
    {
        BranchMutationResult result = await branchService.ActivateAsync(id, cancellationToken);
        return result.Failure == BranchMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Branch!));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, BranchService branchService, CancellationToken cancellationToken)
    {
        BranchMutationResult result = await branchService.DeactivateAsync(id, cancellationToken);
        return result.Failure == BranchMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.Branch!));
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Branch not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "branches.not_found" });

    private static IResult DuplicateProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A branch with this code already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "branches.duplicate_code" });

    private static BranchResponse ToResponse(Persistence.Branch branch) => new(
        branch.Id,
        branch.Code,
        branch.ArabicName,
        branch.EnglishName,
        branch.Description,
        branch.IsActive,
        branch.CreatedAtUtc,
        branch.UpdatedAtUtc);
}
