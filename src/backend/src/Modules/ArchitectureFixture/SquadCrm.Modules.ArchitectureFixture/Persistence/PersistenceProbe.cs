namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// <b>Architecture scaffolding — not a CRM concept.</b> The smallest possible
/// persisted row, existing only to prove that this module owns a schema, a
/// table and its own migrations. Delete it with the fixture once a real module
/// provides equivalent coverage.
/// </summary>
public sealed class PersistenceProbe
{
    /// <summary>Primary key. Supplied by the caller; no database default is configured.</summary>
    public Guid Id { get; init; }

    /// <summary>Free-text marker written by the persistence verification test.</summary>
    public required string Label { get; init; }

    /// <summary>Write timestamp, stored as PostgreSQL <c>timestamptz</c>.</summary>
    public DateTimeOffset RecordedAtUtc { get; init; }
}
