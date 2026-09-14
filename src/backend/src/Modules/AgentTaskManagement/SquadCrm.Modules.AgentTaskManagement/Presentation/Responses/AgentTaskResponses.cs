using System.Text.Json.Serialization;
using SquadCrm.Modules.AgentTaskManagement.Domain.Entities;

namespace SquadCrm.Modules.AgentTaskManagement.Presentation.Responses;

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
    int Version,

    /// <summary>UTC instant of the task's single optional reminder; null when none is set (CRM-144).</summary>
    DateTimeOffset? ReminderAtUtc,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] AgentTaskReminderStatus ReminderStatus,
    DateTimeOffset? ReminderTriggeredAtUtc);
