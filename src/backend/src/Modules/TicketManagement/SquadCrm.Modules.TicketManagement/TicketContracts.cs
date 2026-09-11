using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

public enum TicketSortBy
{
    TicketNumber,
    CreatedAtUtc,
}

/// <summary>
/// Module-local copy of the two-value sort-direction shape (mirrors
/// <c>CustomerManagement</c>'s own copy) — modules do not share plain enums
/// across a project boundary without a contract.
/// </summary>
public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>
/// Bound via <c>[AsParameters]</c> alongside
/// <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/> (mirrors
/// <c>CustomerListQuery</c>). No <c>SubcategoryIds</c>/SLA/Escalation filters —
/// no Subcategory catalog or SLA/Escalation model exists in this repo yet
/// (documented scope gap, see plan).
/// </summary>
public sealed record TicketListQuery(
    string? Search = null,
    TicketStatus[]? Statuses = null,
    Guid[]? CategoryIds = null,
    Guid[]? PriorityIds = null,
    Guid[]? AssigneeIds = null,
    Guid[]? DepartmentIds = null,
    Guid[]? BranchIds = null,
    TicketChannel[]? Channels = null,
    TicketSortBy SortBy = TicketSortBy.TicketNumber,
    SortDirection SortDirection = SortDirection.Asc);

public sealed record CreateTicketRequest(
    [property: Required] Guid CustomerId,
    [property: Required, MaxLength(200)] string Subject,
    [property: Required, MaxLength(4000)] string Description,
    [property: Required] Guid CategoryId,
    Guid? SubcategoryId,
    [property: Required] Guid PriorityId,
    [property: Required] Guid DepartmentId,
    [property: Required] Guid BranchId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketChannel Channel,
    Guid? AssignedAgentId);

public sealed record TicketResponse(
    Guid Id,
    string TicketNumber,
    Guid CustomerId,
    string Subject,
    string Description,
    Guid CategoryId,
    Guid? SubcategoryId,
    Guid PriorityId,
    Guid DepartmentId,
    Guid BranchId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketChannel Channel,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Ticket detail projection (CRM-135). Category/priority names are resolved in
/// this module because both catalogs live in this same DbContext; they are
/// looked up by id WITHOUT an <c>IsActive</c> filter so a historical label
/// never disappears because the reference row was deactivated (BR). A null
/// name means the referenced row no longer exists at all — the UI falls back
/// to the raw id rather than rendering a blank.
/// <para>
/// Customer, department, branch and assigned-agent labels are deliberately NOT
/// resolved here: those belong to other modules, and the story's Deadline
/// Acceptance Override rules out adding cross-module summary infrastructure.
/// The client composes them from each owning module's own API, which
/// authorizes the caller independently.
/// </para>
/// </summary>
public sealed record TicketDetailResponse(
    Guid Id,
    string TicketNumber,
    Guid CustomerId,
    string Subject,
    string Description,
    Guid CategoryId,
    string? CategoryArabicName,
    string? CategoryEnglishName,
    bool? CategoryIsActive,
    Guid? SubcategoryId,
    Guid PriorityId,
    string? PriorityArabicName,
    string? PriorityEnglishName,
    bool? PriorityIsActive,
    int? PriorityRank,
    Guid DepartmentId,
    Guid BranchId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketStatus Status,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketChannel Channel,
    Guid? AssignedAgentId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    int Version);
