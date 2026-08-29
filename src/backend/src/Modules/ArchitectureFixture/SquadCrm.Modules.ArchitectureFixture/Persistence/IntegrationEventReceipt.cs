namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// Fixture-only durable proof that an integration event was consumed. The
/// event identity is the primary key, so at-least-once redelivery cannot apply
/// the fixture effect twice.
/// </summary>
public sealed class IntegrationEventReceipt
{
    public required Guid EventId { get; init; }

    public required string EventType { get; init; }

    public required Guid ProbeId { get; init; }

    public required string ProbeLabel { get; init; }

    public required DateTimeOffset ConsumedAtUtc { get; init; }
}
