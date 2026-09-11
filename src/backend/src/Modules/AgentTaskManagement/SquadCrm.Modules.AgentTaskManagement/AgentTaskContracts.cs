using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SquadCrm.Modules.AgentTaskManagement.Persistence;

namespace SquadCrm.Modules.AgentTaskManagement;

public enum AgentTaskSortBy
{
    DueAtUtc,
    CreatedAtUtc,
}

/// <summary>
/// Module-local copy of the two-value sort-direction shape (mirrors
/// <c>TicketManagement</c>'s own copy) — modules do not share plain enums
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
/// <c>TicketListQuery</c>).
/// </summary>
public sealed record AgentTaskListQuery(
    string? Search = null,
    AgentTaskStatus[]? Statuses = null,
    DateTimeOffset? DueBefore = null,
    DateTimeOffset? DueAfter = null,

    /// <summary>
    /// Restrict the result to tasks owned by the CALLER (CRM-143 "My Tasks").
    /// The owner id is resolved server-side from the authenticated principal,
    /// never supplied by the client, so a caller cannot read another agent's
    /// tasks by editing the query.
    /// </summary>
    bool MyTasksOnly = false,
    AgentTaskSortBy SortBy = AgentTaskSortBy.DueAtUtc,
    SortDirection SortDirection = SortDirection.Asc);

/// <summary>
/// <paramref name="OwnerUserId"/> defaults to the caller when omitted
/// (server-side, via <c>ICurrentUserAccessor</c>). Supplying a different id is
/// accepted only when that id resolves to an eligible (active) staff user —
/// mirrors the eligibility check <c>TicketService.AssignAsync</c> already
/// applies, since no organizational-scope/manager model exists yet (plan).
/// </summary>
public sealed record CreateAgentTaskRequest(
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(4000)] string? Details,
    Guid? OwnerUserId,
    Guid? TicketId,
    Guid? CustomerId,
    DateTimeOffset? DueAtUtc);

/// <summary>
/// <paramref name="Version"/> is the task version the caller last read: a
/// mismatch is rejected instead of silently overwriting a newer change (AC).
/// Ownership is never reassigned through this endpoint (out of scope — no
/// delegation workflow, per the Deadline Acceptance Override).
/// </summary>
public sealed record UpdateAgentTaskRequest(
    [property: Required, MaxLength(200)] string Title,
    [property: MaxLength(4000)] string? Details,
    Guid? TicketId,
    Guid? CustomerId,
    DateTimeOffset? DueAtUtc,
    [property: Required] int Version);

/// <summary>
/// Completion/reopen command. <paramref name="Version"/> is the task version
/// the caller last read: a mismatch is rejected instead of applying a stale
/// action (AC), same rationale as <c>ChangeTicketStatusRequest</c>.
/// </summary>
public sealed record AgentTaskVersionedActionRequest([property: Required] int Version);

public sealed record AgentTaskResponse(
    Guid Id,
    string Title,
    string? Details,
    Guid OwnerUserId,
    Guid? TicketId,
    Guid? CustomerId,
    DateTimeOffset? DueAtUtc,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AgentTaskStatus Status,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    int Version);
