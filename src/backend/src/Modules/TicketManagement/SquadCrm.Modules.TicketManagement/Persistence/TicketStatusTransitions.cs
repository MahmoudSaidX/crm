namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// The fixed ticket lifecycle transition matrix (CRM-137). One place, used by
/// <c>TicketService</c> for authoritative validation and by the detail
/// projection to tell the UI which actions to offer — the UI hint and the
/// enforced rule can therefore never drift apart.
/// <para>
/// Deliberately a fixed domain matrix rather than a configurable lifecycle
/// engine or transition DSL: the story's Deadline Acceptance Override rules
/// those out as stretch scope.
/// </para>
/// </summary>
public static class TicketStatusTransitions
{
    private static readonly IReadOnlyDictionary<TicketStatus, TicketStatus[]> Allowed =
        new Dictionary<TicketStatus, TicketStatus[]>
        {
            [TicketStatus.Open] =
            [
                TicketStatus.InProgress,
                TicketStatus.PendingCustomer,
                TicketStatus.PendingInternal,
                TicketStatus.Resolved,
            ],
            [TicketStatus.InProgress] =
            [
                TicketStatus.PendingCustomer,
                TicketStatus.PendingInternal,
                TicketStatus.Resolved,
            ],
            [TicketStatus.PendingCustomer] =
            [
                TicketStatus.InProgress,
                TicketStatus.PendingInternal,
                TicketStatus.Resolved,
            ],
            [TicketStatus.PendingInternal] =
            [
                TicketStatus.InProgress,
                TicketStatus.PendingCustomer,
                TicketStatus.Resolved,
            ],

            // Resolved and Closed are distinct: resolution precedes closure,
            // and both can be reopened (BR).
            [TicketStatus.Resolved] = [TicketStatus.Closed, TicketStatus.InProgress],
            [TicketStatus.Closed] = [TicketStatus.InProgress],
        };

    /// <summary>
    /// Transitions available from <paramref name="currentStatus"/>. Never
    /// contains <paramref name="currentStatus"/> itself: "no change" is not a
    /// transition, so a double-submit cannot produce a second history row or a
    /// duplicate event.
    /// </summary>
    public static IReadOnlyList<TicketStatus> AllowedFrom(TicketStatus currentStatus) =>
        Allowed.TryGetValue(currentStatus, out TicketStatus[]? targets) ? targets : [];

    public static bool IsAllowed(TicketStatus currentStatus, TicketStatus targetStatus) =>
        AllowedFrom(currentStatus).Contains(targetStatus);

    /// <summary>
    /// Closure and reopen must be justified (Fields Dictionary: "Required for
    /// configured transitions such as reopen/close"). Every other transition
    /// takes an optional reason.
    /// </summary>
    public static bool RequiresReason(TicketStatus currentStatus, TicketStatus targetStatus) =>
        targetStatus == TicketStatus.Closed
        || currentStatus is TicketStatus.Resolved or TicketStatus.Closed;
}
