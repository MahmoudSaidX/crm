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
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

public sealed class TicketManagementModule : IModule
{
    // The "ticketcategories.view"/"ticketcategories.manage" policies are
    // centrally owned and registered by RoleManagementModule (the permission
    // catalog's single home), following the same precedent as
    // DepartmentManagementModule: referenced here by string only, no project
    // reference to RoleManagement.
    public string Name => "TicketManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TicketManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    TicketManagementSchema.MigrationsHistoryTable,
                    TicketManagementSchema.Name)));
        services.AddScoped<TicketCategoryService>();
        services.AddScoped<TicketPriorityService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // IDepartmentActiveLookup is already registered by
        // DepartmentManagementModule; DI resolves those same registrations.
        // No duplicate registration and no project reference to those
        // modules' main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder ticketCategories = endpoints.MapGroup("/api/v1/ticket-categories").WithTags("TicketCategories");

        ticketCategories.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateTicketCategoryRequest>()
            .RequireAuthorization(PermissionPolicies.TicketCategoriesManage);
        ticketCategories.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.TicketCategoriesView);
        ticketCategories.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.TicketCategoriesView);
        ticketCategories.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateTicketCategoryRequest>()
            .RequireAuthorization(PermissionPolicies.TicketCategoriesManage);
        ticketCategories.MapPost("/{id:guid}/activate", ActivateAsync).RequireAuthorization(PermissionPolicies.TicketCategoriesManage);
        ticketCategories.MapPost("/{id:guid}/deactivate", DeactivateAsync).RequireAuthorization(PermissionPolicies.TicketCategoriesManage);

        RouteGroupBuilder ticketPriorities = endpoints.MapGroup("/api/v1/ticket-priorities").WithTags("TicketPriorities");

        ticketPriorities.MapPost("", CreatePriorityAsync).ValidatesDataAnnotations<CreateTicketPriorityRequest>()
            .RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapGet("", ListPrioritiesAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesView);
        ticketPriorities.MapGet("/{id:guid}", GetPriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesView);
        ticketPriorities.MapPut("/{id:guid}", UpdatePriorityAsync).ValidatesDataAnnotations<UpdateTicketPriorityRequest>()
            .RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapPost("/{id:guid}/activate", ActivatePriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
        ticketPriorities.MapPost("/{id:guid}/deactivate", DeactivatePriorityAsync).RequireAuthorization(PermissionPolicies.TicketPrioritiesManage);
    }

    private static async Task<IResult> CreateAsync(
        CreateTicketCategoryRequest request,
        TicketCategoryService ticketCategoryService,
        CancellationToken cancellationToken)
    {
        TicketCategoryMutationResult result = await ticketCategoryService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            TicketCategoryMutationFailure.None => Results.Created(
                $"/api/v1/ticket-categories/{result.TicketCategory!.Id}", ToResponse(result.TicketCategory)),
            TicketCategoryMutationFailure.DuplicateCode => DuplicateProblem(),
            TicketCategoryMutationFailure.InactiveDepartment => InactiveDepartmentProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateTicketCategoryRequest request,
        TicketCategoryService ticketCategoryService,
        CancellationToken cancellationToken)
    {
        TicketCategoryMutationResult result = await ticketCategoryService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            TicketCategoryMutationFailure.None => Results.Ok(ToResponse(result.TicketCategory!)),
            TicketCategoryMutationFailure.NotFound => NotFoundProblem(),
            TicketCategoryMutationFailure.DuplicateCode => DuplicateProblem(),
            TicketCategoryMutationFailure.InactiveDepartment => InactiveDepartmentProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> GetAsync(
        Guid id, TicketCategoryService ticketCategoryService, CancellationToken cancellationToken)
    {
        Persistence.TicketCategory? category = await ticketCategoryService.GetAsync(id, cancellationToken);
        return category is null ? NotFoundProblem() : Results.Ok(ToResponse(category));
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        TicketCategoryService ticketCategoryService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.TicketCategory> page = await ticketCategoryService.ListAsync(pagination, cancellationToken);
        return Results.Ok(new PagedResult<TicketCategoryResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> ActivateAsync(
        Guid id, TicketCategoryService ticketCategoryService, CancellationToken cancellationToken)
    {
        TicketCategoryMutationResult result = await ticketCategoryService.ActivateAsync(id, cancellationToken);
        return result.Failure == TicketCategoryMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.TicketCategory!));
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id, TicketCategoryService ticketCategoryService, CancellationToken cancellationToken)
    {
        TicketCategoryMutationResult result = await ticketCategoryService.DeactivateAsync(id, cancellationToken);
        return result.Failure == TicketCategoryMutationFailure.NotFound ? NotFoundProblem() : Results.Ok(ToResponse(result.TicketCategory!));
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
        Persistence.TicketPriority? priority = await ticketPriorityService.GetAsync(id, cancellationToken);
        return priority is null ? NotFoundPriorityProblem() : Results.Ok(ToResponse(priority));
    }

    private static async Task<IResult> ListPrioritiesAsync(
        [AsParameters] PaginationRequest pagination,
        TicketPriorityService ticketPriorityService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.TicketPriority> page = await ticketPriorityService.ListAsync(pagination, cancellationToken);
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

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ticket category not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketcategories.not_found" });

    private static IResult DuplicateProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A ticket category with this code already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketcategories.duplicate_code" });

    private static IResult InactiveDepartmentProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "The default department is inactive.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketcategories.inactive_department" });

    private static IResult NotFoundPriorityProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ticket priority not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketpriorities.not_found" });

    private static IResult DuplicatePriorityProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "A ticket priority with this code already exists.",
        extensions: new Dictionary<string, object?> { ["code"] = "ticketpriorities.duplicate_code" });

    private static TicketCategoryResponse ToResponse(Persistence.TicketCategory category) => new(
        category.Id,
        category.Code,
        category.ArabicName,
        category.EnglishName,
        category.DefaultDepartmentId,
        category.SortOrder,
        category.IsActive,
        category.CreatedAtUtc,
        category.UpdatedAtUtc);

    private static TicketPriorityResponse ToResponse(Persistence.TicketPriority priority) => new(
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
