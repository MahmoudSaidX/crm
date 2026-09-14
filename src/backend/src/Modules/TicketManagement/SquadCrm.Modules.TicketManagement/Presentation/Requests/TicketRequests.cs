using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SquadCrm.Modules.TicketManagement.Domain.Entities;

namespace SquadCrm.Modules.TicketManagement.Presentation.Requests;

public enum TicketSortBy
{
    TicketNumber,
    CreatedAtUtc,

    /// <summary>
    /// Last material change to the ticket (CRM-141). The agent queue orders by
    /// it so the least recently touched work is easy to find.
    /// </summary>
    UpdatedAtUtc,
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

    /// <summary>
    /// Restrict the result to tickets assigned to the CALLER (CRM-141). The
    /// agent id is resolved server-side from the authenticated principal, never
    /// supplied by the client, so the agent queue cannot be pointed at another
    /// agent by editing the request. It composes with
    /// <see cref="AssigneeIds"/> as an additional AND — neither filter can
    /// widen the other.
    /// </summary>
    bool AssignedToMe = false,
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

/// <summary>
/// Add-internal-note command (CRM-147).
/// <para>
/// <c>MentionedUserIds</c> is an explicit, server-validated id list rather than
/// something parsed out of <c>Body</c>. Free-text <c>@name</c> parsing is named
/// stretch by the story's deadline override, and — more importantly — a parser
/// cannot enforce the Business Rule that a mentioned user must be an eligible
/// teammate: the server validates every id against StaffIdentity and rejects
/// the whole note if any is unknown or inactive, so a mention can never be used
/// to reach someone who should not be reachable.
/// </para>
/// </summary>
public sealed record AddTicketNoteRequest(
    [property: Required]
    [property: StringLength(4000, MinimumLength = 1)]
    string Body,
    Guid[]? MentionedUserIds = null);

/// <summary>Add-watcher command (CRM-147).</summary>
public sealed record AddTicketWatcherRequest([property: Required] Guid UserId);
