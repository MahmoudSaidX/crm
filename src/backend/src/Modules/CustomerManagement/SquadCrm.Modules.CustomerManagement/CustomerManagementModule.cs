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
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

public sealed class CustomerManagementModule : IModule
{
    // The "customers.manage" policy is centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), following
    // the same precedent as AuditModule's AuditViewPolicy: referenced here by
    // string only, no project reference to RoleManagement.
    public string Name => "CustomerManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustomerManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    CustomerManagementSchema.MigrationsHistoryTable,
                    CustomerManagementSchema.Name)));
        services.AddScoped<CustomerService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // IDepartmentActiveLookup/IBranchActiveLookup are already registered
        // by DepartmentManagementModule/BranchManagementModule; DI resolves
        // those same registrations. No duplicate registration and no project
        // reference to those modules' main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder customers = endpoints.MapGroup("/api/v1/customers").WithTags("Customers");

        customers.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateCustomerRequest>()
            .RequireAuthorization(PermissionPolicies.CustomersManage);
        customers.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.CustomersView);
        customers.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.CustomersView);
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

    private static IResult NotFoundProblem() => Results.Problem(
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
        customer.CreatedAtUtc,
        customer.UpdatedAtUtc);
}
