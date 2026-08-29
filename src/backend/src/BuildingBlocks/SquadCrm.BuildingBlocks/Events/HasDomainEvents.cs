using SquadCrm.BuildingBlocks.Abstractions.Events;

namespace SquadCrm.BuildingBlocks.Events;

/// <summary>
/// Opt-in base for an entity that raises domain events inside its own module
/// (AC 1). Deliberately minimal: no dispatcher, no mediator, no bus — a
/// module's own <c>ISaveChangesInterceptor</c> drains <see cref="DomainEvents"/>
/// and decides, per event, whether to translate it into a durable
/// <c>OutboxMessage</c>, then clears them before the underlying <c>SaveChanges</c>
/// call commits.
/// <para>
/// <see cref="ClearDomainEvents"/> is <b>public but drain-only</b>: it exists to
/// be called by the owning module's save-changes interceptor immediately after
/// translating every pending event, never by application/business code. It is
/// not <c>internal</c> because the interceptor that must call it lives in a
/// different assembly per module, and this repo does not use
/// <c>InternalsVisibleTo</c> (a deliberate deviation from a stricter
/// visibility trim, sanctioned by the approved Squad Kit plan's option (a) —
/// see
/// .squad/plans/domain-events-integration-events-transactional-outbox/07-story-crm-198-domain-events-integration-events-transactional-outbox.md,
/// lines ~221-226).
/// </para>
/// </summary>
public abstract class HasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Domain events raised and not yet drained.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    protected void AddDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    /// <summary>Drain-only. See the type-level remarks.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
