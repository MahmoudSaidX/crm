namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// A durable record of one <c>IIntegrationEvent</c>, written in the same
/// database transaction as the business change that caused it (ADR-005).
/// <b>Persistence implementation detail — not a shared/reusable type.</b> Each
/// module that needs an outbox defines and maps its own copy to its own table
/// in its own schema (schema-per-module, ADR-002); there is no shared
/// <c>SquadCrm.BuildingBlocks</c> outbox type — mirrors
/// <c>ArchitectureFixture.Persistence.OutboxMessage</c>'s shape.
/// <para>
/// Only the producer side is built by this story (CRM-133) — no claim/lease/
/// processed columns yet, because no dispatcher/consumer reads this table yet
/// (YAGNI); a later story adds those columns via an additive migration when a
/// real consumer exists, the same way <c>ArchitectureFixtureOutboxJob</c>
/// demonstrates for that module.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>The integration event's own stable, versioned contract name (e.g. <c>"ticket-management.ticket-created.v1"</c>). Durable data.</summary>
    public required string Type { get; init; }

    /// <summary>The event serialized as JSON text (not <c>jsonb</c> — byte-for-byte fidelity, no key reordering).</summary>
    public required string Payload { get; init; }

    /// <summary>Writer's clock at save time — write/ordering time, not the business event's own timestamp (which is inside <see cref="Payload"/>).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    public required string CorrelationId { get; init; }
}
