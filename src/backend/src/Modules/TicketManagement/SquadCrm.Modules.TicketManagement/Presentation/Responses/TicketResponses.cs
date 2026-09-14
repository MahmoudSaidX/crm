using System.Text.Json.Serialization;
using SquadCrm.Modules.TicketManagement.Domain.Entities;

namespace SquadCrm.Modules.TicketManagement.Presentation.Responses;

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
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

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

/// <summary>
/// Who or what produced a timeline entry (CRM-139 Fields Dictionary). The full
/// set is declared because the classification must stay stable as capabilities
/// are added; only <see cref="User"/> and <see cref="Automation"/> are produced
/// today. <see cref="System"/> and <see cref="Integration"/> exist for the
/// SLA/automation (CRM-150/154) and integration (CRM-192+) stories, which
/// append their own rows without reclassifying historical entries.
/// </summary>
public enum TicketTimelineActorType
{
    User,
    System,
    Automation,
    Integration,
}

/// <summary>
/// Explicit visibility classification of one timeline entry (CRM-139 Fields
/// Dictionary). Internal is the default for anything describing internal
/// routing; an entry is only <see cref="Customer"/> when it is safe for a
/// customer to read.
/// </summary>
public enum TicketTimelineVisibility
{
    Internal,
    Customer,
}

/// <summary>
/// Who is asking for the timeline. The internal agent endpoint asks as
/// <see cref="Internal"/>; the customer portal (CRM-171/175) asks as
/// <see cref="Customer"/> and receives only explicitly customer-visible
/// entries, with reasons stripped (AC "internal history is not leaked").
/// </summary>
public enum TicketTimelineAudience
{
    Internal,
    Customer,
}

/// <summary>
/// One entry of a ticket's history timeline (CRM-139).
/// <para>
/// <c>Summary</c> is built only from allow-listed stored values (statuses,
/// levels, target kind, identifiers) — never free text and never a reference
/// -table lookup, so an entry still reads correctly after reference data
/// changes (BR "history records stable identifiers/snapshots"). The stored
/// <c>Reason</c> is carried in its own field rather than folded into the
/// summary, so the customer projection can drop it wholesale.
/// </para>
/// <para>
/// <c>Sequence</c> is the 1-based position in the full ordering for the
/// requested audience, assigned BEFORE paging, so it identifies an entry
/// independently of the page size a caller happened to use.
/// </para>
/// </summary>
public sealed record TicketTimelineEntryResponse(
    Guid EventId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    int Sequence,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketTimelineActorType ActorType,
    string? ActorId,
    string Summary,
    string? Reason,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TicketTimelineVisibility Visibility);

/// <summary>
/// One internal collaboration note (CRM-147). Internal-only: this shape is
/// returned solely by the agent-facing notes endpoint and is never projected
/// into the customer timeline or any portal response (AC 6).
/// </summary>
public sealed record TicketInternalNoteResponse(
    Guid Id,
    Guid TicketId,
    string Body,
    string CreatedBy,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<Guid> MentionedUserIds);

/// <summary>
/// One current watcher of a ticket (CRM-147). The watcher LIST is internal
/// routing information, like assignment, and is never exposed to a customer
/// audience (AC 6).
/// </summary>
public sealed record TicketWatcherResponse(
    Guid UserId,
    string AddedBy,
    DateTimeOffset AddedAtUtc);
