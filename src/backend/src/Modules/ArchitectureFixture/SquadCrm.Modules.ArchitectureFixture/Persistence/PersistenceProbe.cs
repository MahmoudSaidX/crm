using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Events;

namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// <b>Architecture scaffolding — not a CRM concept.</b> The smallest possible
/// persisted row, existing only to prove that this module owns a schema, a
/// table and its own migrations — and, since CRM-198, that it can raise a
/// domain event on write. Delete it with the fixture once a real module
/// provides equivalent coverage.
/// </summary>
public sealed class PersistenceProbe : HasDomainEvents
{
    /// <summary>Primary key. Supplied by the caller; no database default is configured.</summary>
    public required Guid Id { get; init; }

    /// <summary>Free-text marker written by the persistence verification test.</summary>
    public required string Label { get; init; }

    /// <summary>Write timestamp, stored as PostgreSQL <c>timestamptz</c>.</summary>
    public DateTimeOffset RecordedAtUtc { get; init; }

    /// <summary>
    /// The only construction path that raises <see cref="PersistenceProbeRecordedDomainEvent"/>
    /// (AC 1). Object-initializer construction alone (e.g. <c>new PersistenceProbe { ... }</c>)
    /// deliberately does not raise an event — this factory method is the
    /// intentional seam.
    /// </summary>
    public static PersistenceProbe Record(Guid id, string label, DateTimeOffset recordedAtUtc)
    {
        PersistenceProbe probe = new() { Id = id, Label = label, RecordedAtUtc = recordedAtUtc };
        probe.AddDomainEvent(new PersistenceProbeRecordedDomainEvent(id, label, recordedAtUtc));
        return probe;
    }
}

/// <summary>
/// Demonstration-only domain event. Internal: never crosses the module
/// boundary directly — <see cref="ArchitectureFixtureOutboxInterceptor"/>
/// translates it into the public <c>ArchitectureFixtureProbeRecordedIntegrationEvent</c>
/// contract before it is persisted to the outbox.
/// </summary>
internal sealed record PersistenceProbeRecordedDomainEvent(Guid ProbeId, string Label, DateTimeOffset OccurredAtUtc)
    : IDomainEvent;
