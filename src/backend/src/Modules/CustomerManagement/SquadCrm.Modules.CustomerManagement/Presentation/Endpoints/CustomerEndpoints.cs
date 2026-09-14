using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.CustomerManagement.Application.Services;
using SquadCrm.Modules.CustomerManagement.Domain.Entities;
using SquadCrm.Modules.CustomerManagement.Presentation.Requests;
using SquadCrm.Modules.CustomerManagement.Presentation.Responses;

namespace SquadCrm.Modules.CustomerManagement.Presentation.Endpoints;

/// <summary>
/// Customer routes (list, read, create, update, timeline) and the shared
/// customer-not-found mapping. Delegates the ticket-scoped sub-resources to
/// their own endpoint classes so this file stays one cohesive area.
/// </summary>
internal static class CustomerEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder customers = endpoints.MapGroup("/api/v1/customers").WithTags("Customers");

        customers.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateCustomerRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        customers.MapGet("", ListAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .ValidatesDataAnnotations<CustomerListQuery>()
            .RequireAuthorization(PermissionPolicies.CustomersView);
        customers.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        customers.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateCustomerRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        customers.MapGet("/{customerId:guid}/timeline", GetTimelineAsync)
            .RequireAuthorization(PermissionPolicies.CustomersView);

        CustomerContactEndpoints.Map(customers);
        CustomerNoteEndpoints.Map(customers);
        CustomerAttachmentEndpoints.Map(customers);
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] CustomerListQuery query,
        [AsParameters] PaginationRequest pagination,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        PagedResult<Customer> page = await customerService.ListAsync(query, pagination, cancellationToken);
        return Results.Ok(new PagedResult<CustomerResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, CustomerService customerService, CancellationToken cancellationToken)
    {
        Customer? customer = await customerService.GetAsync(id, cancellationToken);
        return customer is null ? NotFoundProblem() : Results.Ok(ToResponse(customer));
    }

    private static async Task<IResult> GetTimelineAsync(
        Guid customerId, CustomerTimelineService timelineService, CancellationToken cancellationToken)
    {
        CustomerTimelineResult result = await timelineService.GetAsync(customerId, cancellationToken);
        return result.Failure switch
        {
            CustomerTimelineFailure.None => Results.Ok(result.Entries!),
            CustomerTimelineFailure.CustomerNotFound => NotFoundProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    internal static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Customer not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.not_found" });

    private static async Task<IResult> CreateAsync(
        CreateCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        CustomerMutationResult result = await customerService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            CustomerMutationFailure.None => Results.Created(
                $"/api/v1/customers/{result.Customer!.Id}", ToResponse(result.Customer)),
            CustomerMutationFailure.DuplicateCustomer => DuplicateProblem(),
            CustomerMutationFailure.InactiveDepartment => InactiveReferenceProblem("department"),
            CustomerMutationFailure.InactiveBranch => InactiveReferenceProblem("branch"),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateCustomerRequest request,
        CustomerService customerService,
        CancellationToken cancellationToken)
    {
        CustomerMutationResult result = await customerService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            CustomerMutationFailure.None => Results.Ok(ToResponse(result.Customer!)),
            CustomerMutationFailure.NotFound => NotFoundProblem(),
            CustomerMutationFailure.InactiveDepartment => InactiveReferenceProblem("department"),
            CustomerMutationFailure.InactiveBranch => InactiveReferenceProblem("branch"),
            CustomerMutationFailure.ConcurrencyConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "This customer was updated by someone else. Reload and try again.",
                extensions: new Dictionary<string, object?> { ["code"] = "customers.update_conflict" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult DuplicateProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A matching customer already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "customers.duplicate_customer" });

    private static IResult InactiveReferenceProblem(string reference) => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: $"The selected {reference} is not active.",
        extensions: new Dictionary<string, object?> { ["code"] = $"customers.inactive_{reference}" });

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.CustomerNumber,
        customer.FirstName,
        customer.LastName,
        customer.PreferredLanguage,
        customer.DepartmentId,
        customer.BranchId,
        customer.Status,
        customer.Version,
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);
}
