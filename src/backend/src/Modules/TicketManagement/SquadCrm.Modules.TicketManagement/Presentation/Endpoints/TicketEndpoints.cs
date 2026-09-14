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
/// Tickets and their collaboration sub-resources (notes, watchers):
/// <c>/api/v1/tickets</c>.
/// </summary>
internal static class TicketEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder tickets = endpoints.MapGroup("/api/v1/tickets").WithTags("Tickets");

        tickets.MapPost("", CreateTicketAsync).ValidatesDataAnnotations<CreateTicketRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsCreate);
        tickets.MapGet("", ListTicketsAsync).RequireAuthorization(PermissionPolicies.TicketsView);
        tickets.MapGet("/{id:guid}", GetTicketAsync).RequireAuthorization(PermissionPolicies.TicketsView);

        // Read-only sub-resource of a ticket, so it reuses "tickets.view"
        // rather than adding a permission (the CRM-129 customer-timeline
        // precedent). A caller who may read the ticket may read its history.
        tickets.MapGet("/{id:guid}/history", GetTicketHistoryAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsView);
        tickets.MapPost("/{id:guid}/assign", AssignTicketAsync).ValidatesDataAnnotations<AssignTicketRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsAssign);
        tickets.MapPost("/{id:guid}/status", ChangeTicketStatusAsync)
            .ValidatesDataAnnotations<ChangeTicketStatusRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsChangeStatus);
        tickets.MapPost("/{id:guid}/escalate", EscalateTicketAsync)
            .ValidatesDataAnnotations<EscalateTicketRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsEscalate);

        // Internal collaboration (CRM-147). Reads reuse "tickets.view" — the
        // CRM-129/139 precedent that a caller who may read a ticket may read
        // its ticket-scoped sub-resources; writes need "tickets.collaborate".
        //
        // There is deliberately NO handoff endpoint here: handing a ticket to
        // another agent is reassignment and stays on /assign above (CRM-136),
        // and department handoff is /escalate — a second ownership path would
        // violate the story's own Business Rule.
        tickets.MapPost("/{id:guid}/notes", AddTicketNoteAsync)
            .ValidatesDataAnnotations<AddTicketNoteRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsCollaborate);
        tickets.MapGet("/{id:guid}/notes", ListTicketNotesAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsView);
        tickets.MapPost("/{id:guid}/watchers", AddTicketWatcherAsync)
            .ValidatesDataAnnotations<AddTicketWatcherRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsCollaborate);
        tickets.MapGet("/{id:guid}/watchers", ListTicketWatchersAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .RequireAuthorization(PermissionPolicies.TicketsView);
        tickets.MapDelete("/{id:guid}/watchers/{userId:guid}", RemoveTicketWatcherAsync)
            .RequireAuthorization(PermissionPolicies.TicketsCollaborate);
    }

    private static async Task<IResult> CreateTicketAsync(
        CreateTicketRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        TicketMutationResult result = await ticketService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            TicketMutationFailure.None => Results.Created(
                $"/api/v1/tickets/{result.Ticket!.Id}", ToResponse(result.Ticket)),
            TicketMutationFailure.InvalidCustomer => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected customer does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.invalid_customer" }),
            TicketMutationFailure.InactiveCategory => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected category is not active.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.inactive_category" }),
            TicketMutationFailure.InactivePriority => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected priority is not active.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.inactive_priority" }),
            TicketMutationFailure.InactiveDepartment => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected department is not active.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.inactive_department" }),
            TicketMutationFailure.InactiveBranch => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected branch is not active.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.inactive_branch" }),
            TicketMutationFailure.DuplicateTicketNumber => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "A ticket with the generated ticket number already exists. Please retry.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.duplicate_ticket_number" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListTicketsAsync(
        [AsParameters] TicketListQuery query,
        [AsParameters] PaginationRequest pagination,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        PagedResult<Ticket> page = await ticketService.ListAsync(query, pagination, cancellationToken);
        return Results.Ok(new PagedResult<TicketResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetTicketAsync(
        Guid id, TicketService ticketService, CancellationToken cancellationToken)
    {
        TicketDetailResponse? detail = await ticketService.GetDetailAsync(id, cancellationToken);
        return detail is null ? NotFoundTicketProblem() : Results.Ok(detail);
    }

    /// <summary>
    /// The internal agent view of a ticket's history (CRM-139), therefore
    /// <see cref="TicketTimelineAudience.Internal"/>. The customer-portal
    /// projection (CRM-171/175) calls the same service with
    /// <see cref="TicketTimelineAudience.Customer"/> from its own endpoint —
    /// it must never filter this one's output client-side.
    /// </summary>
    private static async Task<IResult> GetTicketHistoryAsync(
        Guid id,
        [AsParameters] PaginationRequest pagination,
        TicketTimelineService ticketTimelineService,
        CancellationToken cancellationToken)
    {
        TicketTimelineResult result = await ticketTimelineService.GetAsync(
            id, pagination, TicketTimelineAudience.Internal, cancellationToken);
        return result.Failure switch
        {
            TicketTimelineFailure.None => Results.Ok(result.Page),
            TicketTimelineFailure.TicketNotFound => NotFoundTicketProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> AssignTicketAsync(
        Guid id,
        AssignTicketRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        // Source is fixed to Manual here: this endpoint is the manual action.
        // The automatic-assignment stories (CRM-151/152) call the same service
        // method with Automation from their own trigger.
        TicketMutationResult result = await ticketService.AssignAsync(
            id, request, TicketAssignmentSource.Manual, cancellationToken);
        return result.Failure switch
        {
            TicketMutationFailure.None => Results.Ok(ToResponse(result.Ticket!)),
            TicketMutationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketMutationFailure.IneligibleAgent => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected agent is not an active user.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.ineligible_agent" }),
            TicketMutationFailure.ReasonRequired => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "A reason is required when reassigning a ticket that already has an owner.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.reason_required" }),
            TicketMutationFailure.StaleVersion => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The ticket was changed by someone else. Reload it and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.stale_version" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ChangeTicketStatusAsync(
        Guid id,
        ChangeTicketStatusRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        TicketMutationResult result = await ticketService.ChangeStatusAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            TicketMutationFailure.None => Results.Ok(ToResponse(result.Ticket!)),
            TicketMutationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketMutationFailure.InvalidStatusTransition => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The ticket cannot move to that status from its current status.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.invalid_status_transition" }),
            TicketMutationFailure.ReasonRequired => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "A reason is required when closing or reopening a ticket.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.reason_required" }),
            TicketMutationFailure.StaleVersion => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The ticket was changed by someone else. Reload it and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.stale_version" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> EscalateTicketAsync(
        Guid id,
        EscalateTicketRequest request,
        TicketService ticketService,
        CancellationToken cancellationToken)
    {
        // Source is fixed to Manual here: this endpoint is the manual action.
        // The automatic-escalation stories (CRM-153/154) call the same service
        // method with Automation from their own trigger.
        TicketMutationResult result = await ticketService.EscalateAsync(
            id, request, TicketEscalationSource.Manual, cancellationToken);
        return result.Failure switch
        {
            TicketMutationFailure.None => Results.Ok(ToResponse(result.Ticket!)),
            TicketMutationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketMutationFailure.TicketNotEscalatable => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "A resolved or closed ticket cannot be escalated.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.not_escalatable" }),
            TicketMutationFailure.InvalidEscalationTarget => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected escalation target is not active.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.invalid_escalation_target" }),
            TicketMutationFailure.ReasonRequired => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "A reason is required when escalating a ticket.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.reason_required" }),
            TicketMutationFailure.StaleVersion => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The ticket was changed by someone else. Reload it and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.stale_version" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult NotFoundTicketProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ticket not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "tickets.not_found" });

    private static TicketResponse ToResponse(Ticket ticket) => new(
        ticket.Id,
        ticket.TicketNumber,
        ticket.CustomerId,
        ticket.Subject,
        ticket.Description,
        ticket.CategoryId,
        ticket.SubcategoryId,
        ticket.PriorityId,
        ticket.DepartmentId,
        ticket.BranchId,
        ticket.Status,
        ticket.Channel,
        ticket.AssignedAgentId,
        ticket.EscalationLevel,
        ticket.EscalationTargetType,
        ticket.EscalationTargetId,
        ticket.EscalatedAtUtc,
        ticket.CreatedAtUtc,
        ticket.UpdatedAtUtc);

    private static async Task<IResult> AddTicketNoteAsync(
        Guid id,
        AddTicketNoteRequest request,
        TicketCollaborationService ticketCollaborationService,
        CancellationToken cancellationToken)
    {
        TicketNoteResult result = await ticketCollaborationService.AddNoteAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            TicketCollaborationFailure.None => Results.Created(
                $"/api/v1/tickets/{id}/notes/{result.Note!.Id}", result.Note),
            TicketCollaborationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketCollaborationFailure.IneligibleUser => IneligibleCollaboratorProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListTicketNotesAsync(
        Guid id,
        [AsParameters] PaginationRequest pagination,
        TicketCollaborationService ticketCollaborationService,
        CancellationToken cancellationToken)
    {
        TicketCollaborationListResult<TicketInternalNoteResponse> result =
            await ticketCollaborationService.ListNotesAsync(id, pagination, cancellationToken);
        return result.Failure switch
        {
            TicketCollaborationFailure.None => Results.Ok(result.Page),
            TicketCollaborationFailure.TicketNotFound => NotFoundTicketProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> AddTicketWatcherAsync(
        Guid id,
        AddTicketWatcherRequest request,
        TicketCollaborationService ticketCollaborationService,
        CancellationToken cancellationToken)
    {
        TicketWatcherResult result = await ticketCollaborationService.AddWatcherAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            // 200, not 201: adding an existing watcher is an accepted no-op, so
            // the response describes the resulting membership rather than
            // claiming a row was created.
            TicketCollaborationFailure.None => Results.Ok(result.Watcher),
            TicketCollaborationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketCollaborationFailure.IneligibleUser => IneligibleCollaboratorProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListTicketWatchersAsync(
        Guid id,
        [AsParameters] PaginationRequest pagination,
        TicketCollaborationService ticketCollaborationService,
        CancellationToken cancellationToken)
    {
        TicketCollaborationListResult<TicketWatcherResponse> result =
            await ticketCollaborationService.ListWatchersAsync(id, pagination, cancellationToken);
        return result.Failure switch
        {
            TicketCollaborationFailure.None => Results.Ok(result.Page),
            TicketCollaborationFailure.TicketNotFound => NotFoundTicketProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> RemoveTicketWatcherAsync(
        Guid id,
        Guid userId,
        TicketCollaborationService ticketCollaborationService,
        CancellationToken cancellationToken)
    {
        TicketCollaborationFailure failure =
            await ticketCollaborationService.RemoveWatcherAsync(id, userId, cancellationToken);
        return failure switch
        {
            TicketCollaborationFailure.None => Results.NoContent(),
            TicketCollaborationFailure.TicketNotFound => NotFoundTicketProblem(),
            TicketCollaborationFailure.WatcherNotFound => Results.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "That user is not watching this ticket.",
                extensions: new Dictionary<string, object?> { ["code"] = "tickets.watcher_not_found" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult IneligibleCollaboratorProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "A selected user is not an active user.",
        extensions: new Dictionary<string, object?> { ["code"] = "tickets.ineligible_collaborator" });
}
