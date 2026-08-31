using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;

namespace SquadCrm.Modules.Audit;

public sealed class AuditModule : IModule
{
    // The "audit.view" permission/policy is centrally owned and registered by
    // RoleManagementModule (CRM-113's permission catalog), following the same
    // precedent as StaffIdentityModule's UsersViewPolicy/UsersManagePolicy:
    // referenced here by string only, no project reference to RoleManagement.
    private const string AuditViewPolicy = "permission:audit.view";

    public string Name => "Audit";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    AuditSchema.MigrationsHistoryTable,
                    AuditSchema.Name)));
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<AuditQueryService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auditRecords = endpoints.MapGroup("/api/v1/audit-records").WithTags("Audit");

        auditRecords.MapGet("", ListAsync).RequireAuthorization(AuditViewPolicy);
        auditRecords.MapGet("/{id:long}", GetAsync).RequireAuthorization(AuditViewPolicy);
    }

    private static async Task<IResult> ListAsync(
        [AsParameters] PaginationRequest pagination,
        string? entityType,
        string? action,
        string? actorHandle,
        DateTimeOffset? from,
        DateTimeOffset? to,
        AuditQueryService queryService,
        CancellationToken cancellationToken)
    {
        PagedResult<AuditRecord> page = await queryService.ListAsync(
            pagination, entityType, action, actorHandle, from, to, cancellationToken);
        return Results.Ok(new PagedResult<AuditRecordResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetAsync(
        long id,
        AuditQueryService queryService,
        CancellationToken cancellationToken)
    {
        AuditRecord? record = await queryService.GetAsync(id, cancellationToken);
        return record is null ? NotFoundProblem() : Results.Ok(ToResponse(record));
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Audit record not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "audit.not_found" });

    private static AuditRecordResponse ToResponse(AuditRecord record) => new(
        record.Id,
        record.ActorHandle,
        record.Action,
        record.EntityType,
        record.EntityId,
        record.MetadataJson is null
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.MetadataJson),
        record.OccurredAtUtc);
}

public sealed record AuditRecordResponse(
    long Id,
    string ActorHandle,
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTimeOffset OccurredAtUtc);
