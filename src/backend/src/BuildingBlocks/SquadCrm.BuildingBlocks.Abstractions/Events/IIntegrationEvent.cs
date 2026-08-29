namespace SquadCrm.BuildingBlocks.Abstractions.Events;

/// <summary>
/// Marker for an explicit, versionable cross-module/external contract (ADR-005).
/// Implementations live in each module's own <c>*.Contracts</c> project — never
/// in <c>BuildingBlocks</c>, which stays generic — so a consuming module depends
/// only on the producing module's contracts, never its implementation.
/// Deliberately minimal: exactly the three members below. No routing key, no
/// version-negotiation surface, no headers dictionary — none has a current
/// consumer (YAGNI).
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Stable identity of this specific event occurrence. Copied verbatim into
    /// <c>OutboxMessage.Id</c>. Outbox delivery is at-least-once; this is the
    /// key a future consumer (CRM-199/downstream) dedupes on.
    /// </summary>
    Guid EventId { get; }

    /// <summary>UTC instant the underlying business change occurred.</summary>
    DateTimeOffset OccurredAtUtc { get; }

    /// <summary>
    /// Stable, explicitly-declared, versioned contract name (e.g.
    /// <c>"architecture-fixture.probe-recorded.v1"</c>) — never
    /// <c>nameof(...)</c> or a CLR type name, which breaks on rename/refactor.
    /// This value is durable data (persisted verbatim as <c>OutboxMessage.Type</c>)
    /// and append-only: once published, a version segment is never reused for a
    /// different payload shape.
    /// </summary>
    string Type { get; }
}
