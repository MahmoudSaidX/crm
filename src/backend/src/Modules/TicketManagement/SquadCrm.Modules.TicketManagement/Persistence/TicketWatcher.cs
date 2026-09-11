using SquadCrm.BuildingBlocks.Events;
using SquadCrm.Modules.TicketManagement.Events;

namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>Whether a watcher row was added or removed (CRM-147).</summary>
public enum TicketWatcherAction
{
    Added,
    Removed,
}

/// <summary>
/// Current membership of a ticket's watcher set (CRM-147). A flat set, not a
/// per-event subscription model: AC asks that watchers "receive configured
/// ticket updates", and the durable delivery of those updates belongs to the
/// notification stories (CRM-155/156) which consume this module's outbox. No
/// dispatcher exists project-wide yet, so building a preference model here
/// would be speculative.
/// <para>
/// Membership is a SET — <c>(TicketId, UserId)</c> is unique, so re-adding an
/// existing watcher is a no-op rather than a duplicate row.
/// </para>
/// </summary>
public sealed class TicketWatcher : HasDomainEvents
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public Guid UserId { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string AddedBy { get; init; }

    public DateTimeOffset AddedAtUtc { get; init; }

    public static TicketWatcher Create(
        Guid id, Guid ticketId, Guid userId, string addedBy, DateTimeOffset addedAtUtc)
    {
        TicketWatcher watcher = new()
        {
            Id = id,
            TicketId = ticketId,
            UserId = userId,
            AddedBy = addedBy,
            AddedAtUtc = addedAtUtc,
        };

        watcher.AddDomainEvent(new TicketWatcherChangedDomainEvent(
            ticketId, userId, TicketWatcherAction.Added, addedBy, addedAtUtc));
        return watcher;
    }

    /// <summary>
    /// Raises the removal event on this tracked instance. Called immediately
    /// before the row is deleted: a <c>Deleted</c> entry is still enumerated by
    /// <see cref="TicketManagementOutboxInterceptor"/>, so the outbox row and
    /// the delete commit in the same transaction — the event cannot survive a
    /// rolled-back removal, nor be lost by a successful one.
    /// </summary>
    public void MarkRemoved(string removedBy, DateTimeOffset removedAtUtc) =>
        AddDomainEvent(new TicketWatcherChangedDomainEvent(
            TicketId, UserId, TicketWatcherAction.Removed, removedBy, removedAtUtc));
}

/// <summary>
/// Append-only log of watcher membership changes (CRM-147), mirroring the
/// three history tables CRM-136/137/138 already write.
/// <para>
/// Separate from <see cref="TicketWatcher"/> because removing a watcher
/// deletes the membership row: without this table the fact that someone was
/// removed — and by whom — would vanish from the ticket timeline, which the
/// AC requires and the auditability BR forbids losing.
/// </para>
/// </summary>
public sealed class TicketWatcherHistory
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public Guid UserId { get; init; }
    public TicketWatcherAction Action { get; init; }

    /// <summary>Actor handle, same value the audit trail records.</summary>
    public required string ChangedBy { get; init; }

    public DateTimeOffset ChangedAtUtc { get; init; }
}
