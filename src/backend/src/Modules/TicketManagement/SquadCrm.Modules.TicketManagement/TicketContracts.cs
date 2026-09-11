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

/// <summary>
/// Assignment/reassignment command (CRM-136). <paramref name="Version"/> is the
/// ticket version the caller last read: a mismatch is rejected instead of
/// silently overwriting a newer owner (AC).
/// <para>
/// <c>AssignmentSource</c> is deliberately NOT part of the request — it is
/// server-decided (always <c>Manual</c> on this endpoint) so a client cannot
/// label its own call as an automation and bypass the reassignment-reason rule.
/// </para>
/// </summary>
public sealed record AssignTicketRequest(
    [property: Required] Guid TargetAgentId,
    [property: MaxLength(500)] string? Reason,
    [property: Required] int Version);

/// <summary>
/// Lifecycle status-transition command (CRM-137). <paramref name="Version"/> is
/// the ticket version the caller last read: a mismatch is rejected instead of
/// silently overwriting a newer change (AC). <paramref name="Reason"/> is
/// required by the server for close/reopen transitions only.
/// </summary>
public sealed record ChangeTicketStatusRequest(
    [property: Required, JsonConverter(typeof(JsonStringEnumConverter))] TicketStatus TargetStatus,
    [property: MaxLength(500)] string? Reason,
    [property: Required] int Version);

/// <summary>
/// Manual escalation command (CRM-138). <paramref name="Version"/> is the
/// ticket version the caller last read: a mismatch is rejected instead of
/// applying a second escalation on top of a newer one (AC).
/// <para>
/// The escalation LEVEL is deliberately NOT part of the request — it is
/// server-derived (<c>current + 1</c>), for the same reason
/// <c>AssignTicketRequest</c> omits the assignment source: a client that could
/// name its own level could corrupt the level sequence (AC "prevent unintended
/// duplicate/escalation-level corruption").
/// </para>
/// </summary>
public sealed record EscalateTicketRequest(
    [property: Required, JsonConverter(typeof(JsonStringEnumConverter))] TicketEscalationTargetType TargetType,
    [property: Required] Guid TargetId,
    [property: Required, MaxLength(500)] string Reason,
    [property: Required] int Version);

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
    int EscalationLevel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketEscalationTargetType? EscalationTargetType,
    Guid? EscalationTargetId,
    DateTimeOffset? EscalatedAtUtc,
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
/// <para>
/// The escalation fields (CRM-138) are reported independently of
/// <c>Status</c>: escalation is not a lifecycle status (BR). Level 0 means the
/// ticket is not escalated, and the target fields are then null.
/// </para>
/// <para>
/// <c>AllowedStatusTransitions</c> (CRM-137) is a UX hint so the screen offers
/// only currently valid actions; the transition endpoint re-validates every
/// call against the same matrix, so a client that ignores the hint gains
/// nothing. It carries status NAMES rather than the enum: no global
/// string-enum serializer is configured in this API, so a
/// <c>List&lt;TicketStatus&gt;</c> would serialize as numbers while every
/// scalar status field serializes as a name.
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
    int EscalationLevel,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketEscalationTargetType? EscalationTargetType,
    Guid? EscalationTargetId,
    DateTimeOffset? EscalatedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    int Version,
    IReadOnlyList<string> AllowedStatusTransitions);
