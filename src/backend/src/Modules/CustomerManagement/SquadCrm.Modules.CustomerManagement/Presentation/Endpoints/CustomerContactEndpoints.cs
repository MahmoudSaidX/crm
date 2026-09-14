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
/// Contact sub-resource of a customer: <c>/api/v1/customers/{customerId}/contacts</c>.
/// </summary>
internal static class CustomerContactEndpoints
{
    public static void Map(RouteGroupBuilder customers)
    {
        RouteGroupBuilder contacts = customers.MapGroup("/{customerId:guid}/contacts").WithTags("CustomerContacts");
        contacts.MapPost("", AddContactAsync).ValidatesDataAnnotations<AddCustomerContactRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        contacts.MapGet("", ListContactsAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        contacts.MapPut("/{contactId:guid}", UpdateContactAsync).ValidatesDataAnnotations<UpdateCustomerContactRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        contacts.MapPost("/{contactId:guid}/deactivate", DeactivateContactAsync)
            .RequireAuthorization(PermissionPolicies.CustomersManage);
    }

    private static async Task<IResult> AddContactAsync(
        Guid customerId,
        AddCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.AddAsync(customerId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Created(
                $"/api/v1/customers/{customerId}/contacts/{result.Contact!.Id}", ToContactResponse(result.Contact)),
            CustomerContactMutationFailure.CustomerNotFound => CustomerEndpoints.NotFoundProblem(),
            CustomerContactMutationFailure.InvalidValue => InvalidContactValueProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListContactsAsync(
        Guid customerId, CustomerContactService contactService, CancellationToken cancellationToken)
    {
        List<CustomerContact> contacts = await contactService.ListAsync(customerId, cancellationToken);
        return Results.Ok(contacts.Select(ToContactResponse).ToList());
    }

    private static async Task<IResult> UpdateContactAsync(
        Guid customerId,
        Guid contactId,
        UpdateCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.UpdateAsync(customerId, contactId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Ok(ToContactResponse(result.Contact!)),
            CustomerContactMutationFailure.ContactNotFound => CustomerEndpoints.NotFoundProblem(),
            CustomerContactMutationFailure.InvalidValue => InvalidContactValueProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> DeactivateContactAsync(
        Guid customerId,
        Guid contactId,
        DeactivateCustomerContactRequest request,
        CustomerContactService contactService,
        CancellationToken cancellationToken)
    {
        CustomerContactMutationResult result = await contactService.DeactivateAsync(customerId, contactId, request, cancellationToken);
        return result.Failure switch
        {
            CustomerContactMutationFailure.None => Results.Ok(ToContactResponse(result.Contact!)),
            CustomerContactMutationFailure.ContactNotFound => CustomerEndpoints.NotFoundProblem(),
            CustomerContactMutationFailure.RequiresNewPrimary => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Another active contact of this type must be designated as primary first.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.requires_new_primary" }),
            CustomerContactMutationFailure.InvalidNewPrimary => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected replacement primary contact is not valid.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.invalid_new_primary" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult InvalidContactValueProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "The contact value is not valid for the selected type.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.contacts.invalid_value" });

    private static CustomerContactResponse ToContactResponse(CustomerContact contact) => new(
        contact.Id,
        contact.CustomerId,
        contact.Type,
        contact.Value,
        contact.Label,
        contact.IsPrimary,
        contact.IsActive,
        contact.CreatedAtUtc,
        contact.UpdatedAtUtc);
}
