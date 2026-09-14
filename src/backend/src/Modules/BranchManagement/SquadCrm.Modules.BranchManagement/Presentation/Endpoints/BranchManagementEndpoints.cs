using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.BranchManagement.Application.Services;
using SquadCrm.Modules.BranchManagement.Domain.Entities;
using SquadCrm.Modules.BranchManagement.Presentation.Requests;
using SquadCrm.Modules.BranchManagement.Presentation.Responses;

namespace SquadCrm.Modules.BranchManagement.Presentation.Endpoints;

/// <summary>
/// HTTP surface of the BranchManagement module: route definitions, model binding,
/// status mapping and response projection. Composed by
/// <see cref="BranchManagementModule"/>, which owns registration only.
/// </summary>
internal static class BranchManagementEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder branches = endpoints.MapGroup("/api/v1/branches").WithTags("Branches");

        branches.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateBranchRequest>()
            .RequireAuthorization(PermissionPolicies.BranchesManage);
        branches.MapGet("", ListAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.BranchesView);
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
        Branch? branch = await branchService.GetAsync(id, cancellationToken);
        return branch is null ? NotFoundProblem() : Results.Ok(ToResponse(branch));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        BranchService branchService,
        CancellationToken cancellationToken)
    {
        PagedResult<Branch> page = await branchService.ListAsync(pagination, cancellationToken);
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

    private static BranchResponse ToResponse(Branch branch) => new(
        branch.Id,
        branch.Code,
        branch.ArabicName,
        branch.EnglishName,
        branch.Description,
        branch.IsActive,
        branch.CreatedAtUtc,
        branch.UpdatedAtUtc);
}
