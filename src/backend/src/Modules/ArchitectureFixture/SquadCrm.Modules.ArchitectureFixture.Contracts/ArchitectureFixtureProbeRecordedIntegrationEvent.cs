using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.Modules.ArchitectureFixture.Contracts;

/// <summary>
/// Infrastructure/demo-only integration-event contract. Proves that a domain
/// event raised inside the module is translated into an explicit, versionable
/// cross-module contract before it leaves the module (ADR-005). Not a CRM
/// capability; deleted with the rest of the fixture.
/// </summary>
public sealed record ArchitectureFixtureProbeRecordedIntegrationEvent(
    Guid EventId,
    Guid ProbeId,
    string Label,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    /// <summary>
    /// Stable, versioned contract name — durable data (ADR-005). Append-only:
    /// never reused for a different payload shape; a breaking change ships as
    /// <c>.v2</c>.
    /// </summary>
    public const string ContractName = "architecture-fixture.probe-recorded.v1";

    public string Type => ContractName;
}
