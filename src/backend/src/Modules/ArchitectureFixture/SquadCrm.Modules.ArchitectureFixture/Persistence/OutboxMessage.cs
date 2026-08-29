namespace SquadCrm.Modules.ArchitectureFixture.Persistence;

/// <summary>
/// A durable record of one <c>IIntegrationEvent</c>, written in the same
/// database transaction as the business change that caused it (ADR-005).
/// <b>Persistence implementation detail — not a shared/reusable type.</b> Each
/// module that needs an outbox defines and maps its own copy to its own table
/// in its own schema (schema-per-module, ADR-002); there is no shared
/// <c>SquadCrm.BuildingBlocks</c> outbox type.
/// <para>
/// <see cref="ProcessedAtUtc"/>, <see cref="RetryCount"/> and <see cref="Error"/>
/// are part of this story's Fields Dictionary and are mapped columns, but this
/// story never writes anything other than <c>null</c>/<c>null</c>/<c>0</c> to
/// them — claiming, retrying and marking failure/success is CRM-199's
/// responsibility, once a claim/read-back abstraction exists to call.
/// </para>
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>The integration event's own stable, versioned contract name (e.g. <c>"architecture-fixture.probe-recorded.v1"</c>). Durable data.</summary>
    public required string Type { get; init; }

    /// <summary>The event serialized as JSON text (not <c>jsonb</c> — byte-for-byte fidelity, no key reordering).</summary>
    public required string Payload { get; init; }

    /// <summary>Writer's clock at save time — write/ordering time, not the business event's own timestamp (which is inside <see cref="Payload"/>).</summary>
    public required DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>Null until a future story's processor marks this message delivered. Always null as written by this story.</summary>
    public DateTimeOffset? ProcessedAtUtc { get; private set; }

    /// <summary>Always 0 as written by this story. A future story's processor increments this on failure.</summary>
    public int RetryCount { get; private set; }

    /// <summary>Always null as written by this story. Truncated/sanitised at the write site by whichever story writes it — never a secret/PII.</summary>
    public string? Error { get; private set; }

    public required string CorrelationId { get; init; }

    public Guid? LeaseId { get; private set; }

    public DateTimeOffset? LeasedUntilUtc { get; private set; }

    public DateTimeOffset? NextAttemptAtUtc { get; private set; }

    internal void MarkProcessed(Guid leaseId, DateTimeOffset processedAtUtc)
    {
        EnsureLeaseOwner(leaseId);
        ProcessedAtUtc = processedAtUtc;
        Error = null;
        LeaseId = null;
        LeasedUntilUtc = null;
        NextAttemptAtUtc = null;
    }

    internal void MarkFailed(Guid leaseId, string error, DateTimeOffset? nextAttemptAtUtc)
    {
        EnsureLeaseOwner(leaseId);
        RetryCount++;
        Error = error.Length <= 2000 ? error : error[..2000];
        LeaseId = null;
        LeasedUntilUtc = null;
        NextAttemptAtUtc = nextAttemptAtUtc;
    }

    private void EnsureLeaseOwner(Guid leaseId)
    {
        if (LeaseId != leaseId)
        {
            throw new InvalidOperationException($"Outbox message '{Id}' is not owned by lease '{leaseId}'.");
        }
    }
}
