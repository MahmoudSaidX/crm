namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

/// <summary>
/// A durable record of one <c>IIntegrationEvent</c>, written in the same
/// database transaction as the business change that caused it (ADR-005).
/// <b>Persistence implementation detail — not a shared/reusable type.</b> Mirrors
/// <c>TicketManagement.Persistence.OutboxMessage</c>'s shape exactly
/// (schema-per-module, ADR-002; no shared outbox type).
/// <para>
/// Only the producer side is built by this story (CRM-143) — no claim/lease/
/// processed columns yet, because no dispatcher/consumer reads this table yet
/// (YAGNI); CRM-144 (Agent Task Reminders) is the future consumer.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }
}
