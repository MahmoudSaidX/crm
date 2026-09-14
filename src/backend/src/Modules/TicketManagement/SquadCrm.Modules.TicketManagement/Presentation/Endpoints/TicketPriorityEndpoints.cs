using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.TicketManagement.Application.Services;
using SquadCrm.Modules.TicketManagement.Domain.Entities;
using SquadCrm.Modules.TicketManagement.Presentation.Requests;
using SquadCrm.Modules.TicketManagement.Presentation.Responses;

namespace SquadCrm.Modules.TicketManagement.Presentation.Endpoints;

/// <summary>
/// Ticket priority catalog: <c>/api/v1/ticket-priorities</c>.
/// </summary>
internal static class TicketPriorityEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder ticketPriorities = endpoints.MapGroup("/api/v1/ticket-priorities").WithTags("TicketPriorities");

        ticketPriorities.MapPost("", CreatePriorityAsync).ValidatesDataAnnotations<CreateTicketPriorityRequest>()
            .RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapGet("", ListPrioritiesAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.TicketPrioritiesView);
        ticketPriorities.MapGet("/{id:guid}", GetPriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesView);
        ticketPriorities.MapPut("/{id:guid}", UpdatePriorityAsync).ValidatesDataAnnotations<UpdateTicketPriorityRequest>()
            .RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapPost("/{id:guid}/activate", ActivatePriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapPost("/{id:guid}/deactivate", DeactivatePriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
    }

    private static async Task<IResult> CreatePriorityAsync(
        CreateTicketPriorityRequest request,
        TicketPriorityService ticketPriorityService,
        CancellationToken cancellationToken)
    {
        TicketPriorityMutationResult result = await ticketPriorityService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            TicketPriorityMutationFailure.None => Results.Created(
                $"/api/v1/ticket-priorities/{result.TicketPriority!.Id}", ToResponse(result.TicketPriority)),
            TicketPriorityMutationFailure.DuplicateCode => DuplicatePriorityProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdatePriorityAsync(
        Guid id,
        UpdateTicketPriorityRequest request,
        TicketPriorityService ticketPriorityService,
        CancellationToken cancellationToken)
    {
        TicketPriorityMutationResult result = await ticketPriorityService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            TicketPriorityMutationFailure.None => Results.Ok(ToResponse(result.TicketPriority!)),
            TicketPriorityMutationFailure.NotFound => NotFoundPriorityProblem(),
            TicketPriorityMutationFailure.DuplicateCode => DuplicatePriorityProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> GetPriorityAsync(
        Guid id, TicketPriorityService ticketPriorityService, CancellationToken cancellationToken)
    {
        TicketPriority? priority = await ticketPriorityService.GetAsync(id, cancellationToken);
        return priority is null ? NotFoundPriorityProblem() : Results.Ok(ToResponse(priority));
    }

    private static async Task<IResult> ListPrioritiesAsync(
        [AsParameters] PaginationRequest pagination,
        TicketPriorityService ticketPriorityService,
        CancellationToken cancellationToken)
    {
        PagedResult<TicketPriority> page = await ticketPriorityService.ListAsync(pagination, cancellationToken);
        return Results.Ok(new PagedResult<TicketPriorityResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> ActivatePriorityAsync(
        Guid id, TicketPriorityService ticketPriorityService, CancellationToken cancellationToken)
    {
        TicketPriorityMutationResult result = await ticketPriorityService.ActivateAsync(id, cancellationToken);
        return result.Failure == TicketPriorityMutationFailure.NotFound ? NotFoundPriorityProblem() : Results.Ok(ToResponse(result.TicketPriority!));
    }

    private static async Task<IResult> DeactivatePriorityAsync(
        Guid id, TicketPriorityService ticketPriorityService, CancellationToken cancellationToken)
    {
        TicketPriorityMutationResult result = await ticketPriorityService.DeactivateAsync(id, cancellationToken);
        return result.Failure == TicketPriorityMutationFailure.NotFound ? NotFoundPriorityProblem() : Results.Ok(ToResponse(result.TicketPriority!));
    }

    private static IResult NotFoundPriorityProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ticket priority not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketpriorities.not_found" });

    private static IResult DuplicatePriorityProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A ticket priority with this code already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketpriorities.duplicate_code" });

    private static TicketPriorityResponse ToResponse(TicketPriority priority) => new(
        priority.Id,
        priority.Code,
        priority.ArabicName,
        priority.EnglishName,
        priority.Rank,
        priority.Description,
        priority.IsActive,
        priority.CreatedAtUtc,
        priority.UpdatedAtUtc);
}
