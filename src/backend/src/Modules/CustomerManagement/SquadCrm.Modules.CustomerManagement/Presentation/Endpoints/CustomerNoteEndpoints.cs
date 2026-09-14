using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.CustomerManagement.Application.Services;
using SquadCrm.Modules.CustomerManagement.Domain.Entities;
using SquadCrm.Modules.CustomerManagement.Presentation.Requests;
using SquadCrm.Modules.CustomerManagement.Presentation.Responses;

namespace SquadCrm.Modules.CustomerManagement.Presentation.Endpoints;

/// <summary>
/// Note sub-resource of a customer: <c>/api/v1/customers/{customerId}/notes</c>.
/// </summary>
internal static class CustomerNoteEndpoints
{
    public static void Map(RouteGroupBuilder customers)
    {
        RouteGroupBuilder notes = customers.MapGroup("/{customerId:guid}/notes").WithTags("CustomerNotes");
        notes.MapPost("", AddNoteAsync).ValidatesDataAnnotations<AddCustomerNoteRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        notes.MapGet("", ListNotesAsync).RequireAuthorization(PermissionPolicies.CustomersView);
    }

    private static async Task<IResult> AddNoteAsync(
        Guid customerId,
        AddCustomerNoteRequest request,
        CustomerNoteService noteService,
        CancellationToken cancellationToken)
    {
        CustomerNoteMutationResult result = await noteService.AddAsync(customerId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerNoteMutationFailure.None => Results.Created(
                $"/api/v1/customers/{customerId}/notes/{result.Note!.Id}", ToNoteResponse(result.Note)),
            CustomerNoteMutationFailure.CustomerNotFound => CustomerEndpoints.NotFoundProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListNotesAsync(
        Guid customerId, CustomerNoteService noteService, CancellationToken cancellationToken)
    {
        List<CustomerNote> notes = await noteService.ListAsync(customerId, cancellationToken);
        return Results.Ok(notes.Select(ToNoteResponse).ToList());
    }

    private static CustomerNoteResponse ToNoteResponse(CustomerNote note) => new(
        note.Id,
        note.CustomerId,
        note.Body,
        note.AuthorUserId,
        note.CreatedAtUtc);
}
