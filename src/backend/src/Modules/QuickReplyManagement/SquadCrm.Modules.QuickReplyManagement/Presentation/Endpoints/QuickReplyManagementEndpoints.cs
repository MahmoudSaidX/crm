using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.QuickReplyManagement.Application.Services;
using SquadCrm.Modules.QuickReplyManagement.Domain.Entities;
using SquadCrm.Modules.QuickReplyManagement.Presentation.Requests;
using SquadCrm.Modules.QuickReplyManagement.Presentation.Responses;

namespace SquadCrm.Modules.QuickReplyManagement.Presentation.Endpoints;

/// <summary>
/// HTTP surface of the QuickReplyManagement module: route definitions, model binding,
/// status mapping and response projection. Composed by
/// <see cref="QuickReplyManagementModule"/>, which owns registration only.
/// </summary>
internal static class QuickReplyManagementEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder quickReplies = endpoints.MapGroup("/api/v1/quick-replies").WithTags("QuickReplies");

        // Every write route requires quickreplies.manage as a floor. The
        // scope-dependent half of the rule — the global-template permission
        // for Global templates, ownership for Personal ones — cannot be
        // expressed here (a create carries its scope in the body; an update
        // targets a row whose scope is known only once loaded), so
        // QuickReplyService applies it after the scope is known.
        quickReplies.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateQuickReplyRequest>()
            .RequireAuthorization(PermissionPolicies.QuickRepliesManage);
        quickReplies.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.QuickRepliesView);
        quickReplies.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.QuickRepliesView);
        quickReplies.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateQuickReplyRequest>()
            .RequireAuthorization(PermissionPolicies.QuickRepliesManage);
        quickReplies.MapPost("/{id:guid}/activate", ActivateAsync)
            .RequireAuthorization(PermissionPolicies.QuickRepliesManage);
        quickReplies.MapPost("/{id:guid}/deactivate", DeactivateAsync)
            .RequireAuthorization(PermissionPolicies.QuickRepliesManage);
    }

    private static async Task<IResult> CreateAsync(
        CreateQuickReplyRequest request,
        QuickReplyService quickReplyService,
        CancellationToken cancellationToken)
    {
        QuickReplyMutationResult result = await quickReplyService.CreateAsync(request, cancellationToken);
        return result.Failure == QuickReplyMutationFailure.None
            ? Results.Created($"/api/v1/quick-replies/{result.QuickReply!.Id}", ToResponse(result.QuickReply))
            : Problem(result.Failure);
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] QuickReplyListQuery query,
        [AsParameters] PaginationRequest pagination,
        QuickReplyService quickReplyService,
        CancellationToken cancellationToken)
    {
        PagedResult<QuickReply> page =
            await quickReplyService.ListAsync(query, pagination, cancellationToken);
        return Results.Ok(new PagedResult<QuickReplyResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, QuickReplyService quickReplyService, CancellationToken cancellationToken)
    {
        QuickReplyMutationResult result = await quickReplyService.GetAsync(id, cancellationToken);
        return result.Failure == QuickReplyMutationFailure.None
            ? Results.Ok(ToResponse(result.QuickReply!))
            : Problem(result.Failure);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateQuickReplyRequest request,
        QuickReplyService quickReplyService,
        CancellationToken cancellationToken)
    {
        QuickReplyMutationResult result = await quickReplyService.UpdateAsync(id, request, cancellationToken);
        return OkOrProblem(result);
    }

    private static async Task<IResult> ActivateAsync(
        Guid id, QuickReplyService quickReplyService, CancellationToken cancellationToken) =>
        OkOrProblem(await quickReplyService.ActivateAsync(id, cancellationToken));

    private static async Task<IResult> DeactivateAsync(
        Guid id, QuickReplyService quickReplyService, CancellationToken cancellationToken) =>
        OkOrProblem(await quickReplyService.DeactivateAsync(id, cancellationToken));

    private static IResult OkOrProblem(QuickReplyMutationResult result) =>
        result.Failure == QuickReplyMutationFailure.None
            ? Results.Ok(ToResponse(result.QuickReply!))
            : Problem(result.Failure);

    private static IResult Problem(QuickReplyMutationFailure failure) => failure switch
    {
        QuickReplyMutationFailure.NotFound => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Quick reply not found.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.not_found" }),
        QuickReplyMutationFailure.DuplicateName => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "A quick reply with this name already exists in this scope.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.duplicate_name" }),
        QuickReplyMutationFailure.GlobalPermissionRequired => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Managing global quick replies requires the global template permission.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.global_permission_required" }),
        QuickReplyMutationFailure.NotOwner => Results.Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "You do not own this quick reply.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.not_owner" }),
        QuickReplyMutationFailure.ContentRequired => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Arabic or English content is required.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.content_required" }),
        QuickReplyMutationFailure.CallerUnresolved => Results.Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "The current user could not be resolved.",
            extensions: new Dictionary<string, object?> { ["code"] = "quickreplies.caller_unresolved" }),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
    };

    private static QuickReplyResponse ToResponse(QuickReply quickReply) => new(
        quickReply.Id,
        quickReply.Name,
        quickReply.ArabicContent,
        quickReply.EnglishContent,
        quickReply.Scope,
        quickReply.OwnerUserId,
        quickReply.IsActive,
        quickReply.CreatedAtUtc,
        quickReply.UpdatedAtUtc);
}
