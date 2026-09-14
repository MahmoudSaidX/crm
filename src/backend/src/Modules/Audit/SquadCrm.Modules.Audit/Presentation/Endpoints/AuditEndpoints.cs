using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.Modules.Audit.Application.Services;
using SquadCrm.Modules.Audit.Domain.Entities;

using SquadCrm.Modules.Audit.Presentation.Responses;

namespace SquadCrm.Modules.Audit.Presentation.Endpoints;

/// <summary>
/// HTTP surface of the Audit module: route definitions, model binding,
/// status mapping and response projection. Composed by
/// <see cref="AuditModule"/>, which owns registration only.
/// </summary>
internal static class AuditEndpoints
{
    // The "audit.view" permission/policy is centrally owned and registered by
    // RoleManagementModule (CRM-113's permission catalog), following the same
    // precedent as StaffIdentityModule's UsersViewPolicy/UsersManagePolicy:
    // referenced here by string only, no project reference to RoleManagement.
    private const string AuditViewPolicy = "permission:audit.view";

    public static void Map(IEndpointRouteBuilder endpoints)
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
