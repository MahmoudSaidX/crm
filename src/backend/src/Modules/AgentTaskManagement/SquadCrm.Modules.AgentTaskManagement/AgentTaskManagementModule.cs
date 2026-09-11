using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.AgentTaskManagement.Persistence;

namespace SquadCrm.Modules.AgentTaskManagement;

public sealed class AgentTaskManagementModule : IModule
{
    // The "tasks.*" policies are centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), same
    // precedent as TicketManagement: referenced here by string only, no
    // project reference to RoleManagement.
    public string Name => "AgentTaskManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // ICorrelationIdAccessor is resolved from the container the HOST
        // registers it in (Program.cs) — mirrors TicketManagementModule.
        services.AddDbContext<AgentTaskManagementDbContext>((serviceProvider, options) =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    AgentTaskManagementSchema.MigrationsHistoryTable,
                    AgentTaskManagementSchema.Name))
                .AddInterceptors(new AgentTaskManagementOutboxInterceptor(
                    serviceProvider.GetRequiredService<ICorrelationIdAccessor>())));
        services.AddScoped<AgentTaskService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // ICustomerExistsLookup by CustomerManagementModule; ITicketExistsLookup
        // by TicketManagementModule; IStaffSubjectReferenceReader by
        // StaffIdentityModule. DI resolves those same registrations. No
        // duplicate registration and no project reference to those modules'
        // main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder tasks = endpoints.MapGroup("/api/v1/tasks").WithTags("AgentTasks");

        tasks.MapPost("", CreateAsync).ValidatesDataAnnotations<CreateAgentTaskRequest>()
            .RequireAuthorization(PermissionPolicies.TasksCreate);
        tasks.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.TasksView);
        tasks.MapGet("/{id:guid}", GetAsync).RequireAuthorization(PermissionPolicies.TasksView);
        tasks.MapPut("/{id:guid}", UpdateAsync).ValidatesDataAnnotations<UpdateAgentTaskRequest>()
            .RequireAuthorization(PermissionPolicies.TasksEdit);
        tasks.MapPost("/{id:guid}/complete", CompleteAsync)
            .ValidatesDataAnnotations<AgentTaskVersionedActionRequest>()
            .RequireAuthorization(PermissionPolicies.TasksComplete);
        tasks.MapPost("/{id:guid}/reopen", ReopenAsync)
            .ValidatesDataAnnotations<AgentTaskVersionedActionRequest>()
            .RequireAuthorization(PermissionPolicies.TasksComplete);
    }

    private static async Task<IResult> CreateAsync(
        CreateAgentTaskRequest request,
        AgentTaskService agentTaskService,
        CancellationToken cancellationToken)
    {
        AgentTaskMutationResult result = await agentTaskService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            AgentTaskMutationFailure.None => Results.Created(
                $"/api/v1/tasks/{result.AgentTask!.Id}", ToResponse(result.AgentTask)),
            AgentTaskMutationFailure.OwnerUnresolved => OwnerUnresolvedProblem(),
            AgentTaskMutationFailure.IneligibleOwner => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected owner is not an active user.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.ineligible_owner" }),
            AgentTaskMutationFailure.InvalidTicket => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected ticket does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.invalid_ticket" }),
            AgentTaskMutationFailure.InvalidCustomer => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected customer does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.invalid_customer" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] AgentTaskListQuery query,
        [AsParameters] PaginationRequest pagination,
        AgentTaskService agentTaskService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.AgentTask> page = await agentTaskService.ListAsync(query, pagination, cancellationToken);
        return Results.Ok(new PagedResult<AgentTaskResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        Guid id, AgentTaskService agentTaskService, CancellationToken cancellationToken)
    {
        AgentTaskMutationResult result = await agentTaskService.GetAsync(id, cancellationToken);
        return result.Failure switch
        {
            AgentTaskMutationFailure.None => Results.Ok(ToResponse(result.AgentTask!)),
            AgentTaskMutationFailure.TaskNotFound => NotFoundProblem(),
            AgentTaskMutationFailure.NotOwner => ForbiddenProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UpdateAsync(
        Guid id,
        UpdateAgentTaskRequest request,
        AgentTaskService agentTaskService,
        CancellationToken cancellationToken)
    {
        AgentTaskMutationResult result = await agentTaskService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            AgentTaskMutationFailure.None => Results.Ok(ToResponse(result.AgentTask!)),
            AgentTaskMutationFailure.TaskNotFound => NotFoundProblem(),
            AgentTaskMutationFailure.NotOwner => ForbiddenProblem(),
            AgentTaskMutationFailure.StaleVersion => StaleVersionProblem(),
            AgentTaskMutationFailure.InvalidTicket => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected ticket does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.invalid_ticket" }),
            AgentTaskMutationFailure.InvalidCustomer => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The selected customer does not exist.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.invalid_customer" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> CompleteAsync(
        Guid id,
        AgentTaskVersionedActionRequest request,
        AgentTaskService agentTaskService,
        CancellationToken cancellationToken)
    {
        AgentTaskMutationResult result = await agentTaskService.CompleteAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            AgentTaskMutationFailure.None => Results.Ok(ToResponse(result.AgentTask!)),
            AgentTaskMutationFailure.TaskNotFound => NotFoundProblem(),
            AgentTaskMutationFailure.NotOwner => ForbiddenProblem(),
            AgentTaskMutationFailure.StaleVersion => StaleVersionProblem(),
            AgentTaskMutationFailure.AlreadyCompleted => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The task is already completed.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.already_completed" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ReopenAsync(
        Guid id,
        AgentTaskVersionedActionRequest request,
        AgentTaskService agentTaskService,
        CancellationToken cancellationToken)
    {
        AgentTaskMutationResult result = await agentTaskService.ReopenAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            AgentTaskMutationFailure.None => Results.Ok(ToResponse(result.AgentTask!)),
            AgentTaskMutationFailure.TaskNotFound => NotFoundProblem(),
            AgentTaskMutationFailure.NotOwner => ForbiddenProblem(),
            AgentTaskMutationFailure.StaleVersion => StaleVersionProblem(),
            AgentTaskMutationFailure.NotCompleted => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "The task is not completed.",
                extensions: new Dictionary<string, object?> { ["code"] = "tasks.not_completed" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Task not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "tasks.not_found" });

    private static IResult ForbiddenProblem() => Results.Problem(
        statusCode: StatusCodes.Status403Forbidden,
        title: "You do not own this task.",
        extensions: new Dictionary<string, object?> { ["code"] = "tasks.not_owner" });

    private static IResult StaleVersionProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "The task was changed by someone else. Reload it and try again.",
        extensions: new Dictionary<string, object?> { ["code"] = "tasks.stale_version" });

    private static IResult OwnerUnresolvedProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "The current user could not be resolved as a task owner.",
        extensions: new Dictionary<string, object?> { ["code"] = "tasks.owner_unresolved" });

    private static AgentTaskResponse ToResponse(Persistence.AgentTask task) => new(
        task.Id,
        task.Title,
        task.Details,
        task.OwnerUserId,
        task.TicketId,
        task.CustomerId,
        task.DueAtUtc,
        task.Status,
        task.CompletedAtUtc,
        task.CreatedAtUtc,
        task.UpdatedAtUtc,
        task.Version);
}
