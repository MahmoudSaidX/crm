namespace SquadCrm.BuildingBlocks.Abstractions.Events;

/// <summary>
/// Marker for an event raised and consumed <b>inside the owning module only</b>.
/// A domain event never crosses a module boundary; when another module or an
/// external consumer needs to know, the owning module translates it into an
/// explicit <see cref="IIntegrationEvent"/> (ADR-005).
/// </summary>
public interface IDomainEvent
{
    /// <summary>UTC instant the event was raised.</summary>
    DateTimeOffset OccurredAtUtc { get; }
}
