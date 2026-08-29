using SquadCrm.BuildingBlocks.Abstractions.Events;
using SquadCrm.BuildingBlocks.Events;

namespace SquadCrm.UnitTests;

public sealed class DomainEventTests
{
    [Fact]
    public void Entity_RecordsEventsInOrder_AndDrainsThemExplicitly()
    {
        DateTimeOffset firstOccurredAt = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);
        DateTimeOffset secondOccurredAt = firstOccurredAt.AddMinutes(1);
        TestEntity entity = new();

        entity.Raise(firstOccurredAt);
        entity.Raise(secondOccurredAt);

        Assert.Equal(
            [firstOccurredAt, secondOccurredAt],
            entity.DomainEvents.Select(domainEvent => domainEvent.OccurredAtUtc));

        entity.ClearDomainEvents();

        Assert.Empty(entity.DomainEvents);
    }

    private sealed class TestEntity : HasDomainEvents
    {
        public void Raise(DateTimeOffset occurredAtUtc) =>
            AddDomainEvent(new TestDomainEvent(occurredAtUtc));
    }

    private sealed record TestDomainEvent(DateTimeOffset OccurredAtUtc) : IDomainEvent;
}
