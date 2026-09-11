using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.DemoData;

/// <summary>Which demo actor a step acts as / assigns to.</summary>
public enum DemoTicketActor
{
    Agent1,
    Agent2,
    Manager,
    Admin,
}

/// <summary>One scripted step in a demo ticket's life.</summary>
public abstract record DemoTicketStep(int HourOffset)
{
    public sealed record Assignment(int HourOffset, DemoTicketActor Target, DemoTicketActor ActedBy, string? Reason)
        : DemoTicketStep(HourOffset);

    public sealed record StatusChange(int HourOffset, TicketStatus Target, DemoTicketActor ActedBy, string? Reason)
        : DemoTicketStep(HourOffset);

    public sealed record Escalation(
        int HourOffset,
        TicketEscalationTargetType TargetType,
        DemoTicketActor? AgentTarget,
        DemoTicketActor ActedBy,
        string Reason) : DemoTicketStep(HourOffset);
}

/// <summary>A whole demo ticket: when it was created and what happened to it.</summary>
public sealed record DemoTicketPlan(int AgeInDays, int CreatedHour, IReadOnlyList<DemoTicketStep> Steps);

/// <summary>
/// Deterministic generator for demo ticket histories.
/// <para>
/// Pure and side-effect free so it can be unit-tested without a database. It
/// validates every status step against the authoritative
/// <see cref="TicketStatusTransitions"/> matrix and supplies a reason wherever
/// <see cref="TicketStatusTransitions.RequiresReason"/> demands one, so the
/// seeder can never write a history the domain would have rejected. Escalation
/// steps are never emitted once a ticket has reached
/// <see cref="TicketStatus.Resolved"/> or <see cref="TicketStatus.Closed"/>,
/// matching <c>TicketService.EscalateAsync</c>'s eligibility rule.
/// </para>
/// </summary>
public static class DemoTicketScript
{
    private const string ProgressReason = "Agent started working the ticket.";
    private const string WaitingCustomerReason = "Waiting for the customer to confirm the account details.";
    private const string WaitingInternalReason = "Waiting for the billing team to confirm the refund.";
    private const string ResolveReason = "Issue resolved and confirmed with the customer.";
    private const string CloseReason = "Closed after the customer confirmed the resolution.";
    private const string ReassignReason = "Reassigned to the agent who owns this product area.";
    private const string EscalationReason = "Breached the expected response time; escalating for attention.";
    private const string SecondEscalationReason = "Still unresolved after the first escalation; raising the level.";

    /// <summary>
    /// The plan for the ticket at <paramref name="index"/>. The shape is chosen
    /// from the index (not from <paramref name="random"/>) so the status mix is
    /// exactly the intended distribution, while ages, hours and actors come from
    /// the seeded <paramref name="random"/>.
    /// </summary>
    public static DemoTicketPlan For(int index, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        int ageInDays = random.Next(0, 90);
        int createdHour = random.Next(6, 18);
        int shape = index % 100;
        DemoTicketActor owner = index % 2 == 0 ? DemoTicketActor.Agent1 : DemoTicketActor.Agent2;
        DemoTicketActor other = owner == DemoTicketActor.Agent1 ? DemoTicketActor.Agent2 : DemoTicketActor.Agent1;

        List<DemoTicketStep> steps = [];
        TicketStatus status = TicketStatus.Open;
        int escalationLevel = 0;
        int hour = 1 + random.Next(0, 6);

        void Assign(DemoTicketActor target, DemoTicketActor actedBy, bool replacingOwner)
        {
            steps.Add(new DemoTicketStep.Assignment(
                hour, target, actedBy, replacingOwner ? ReassignReason : null));
            hour += 1 + random.Next(1, 8);
        }

        void Move(TicketStatus target, DemoTicketActor actedBy)
        {
            if (!TicketStatusTransitions.IsAllowed(status, target))
            {
                throw new InvalidOperationException(
                    $"Demo script produced an illegal transition {status} -> {target}.");
            }

            string? reason = TicketStatusTransitions.RequiresReason(status, target)
                ? ReasonFor(status, target)
                : null;
            steps.Add(new DemoTicketStep.StatusChange(hour, target, actedBy, reason));
            status = target;
            hour += 1 + random.Next(2, 20);
        }

        void Escalate(TicketEscalationTargetType targetType, DemoTicketActor? agentTarget, DemoTicketActor actedBy)
        {
            // Same eligibility rule TicketService enforces.
            if (status is TicketStatus.Resolved or TicketStatus.Closed)
            {
                return;
            }

            escalationLevel++;
            steps.Add(new DemoTicketStep.Escalation(
                hour,
                targetType,
                agentTarget,
                actedBy,
                escalationLevel == 1 ? EscalationReason : SecondEscalationReason));
            hour += 1 + random.Next(2, 12);
        }

        switch (shape)
        {
            // ~20%: untouched queue. Half of them are not even assigned yet, which
            // is what makes the "unassigned" filter meaningful.
            case < 10:
                break;
            case < 15:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                break;

            // ~5%: owned by the support manager rather than an agent, so the
            // queue is not made up of exactly two owners.
            case < 20:
                Assign(DemoTicketActor.Manager, DemoTicketActor.Admin, replacingOwner: false);
                break;

            // ~25%: assigned and in progress — the bulk of an active queue.
            case < 45:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                break;

            // ~10%: currently waiting on the customer.
            case < 55:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Move(TicketStatus.PendingCustomer, owner);
                break;

            // ~10%: bounced off the customer and back.
            case < 65:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Move(TicketStatus.PendingCustomer, owner);
                Move(TicketStatus.InProgress, owner);
                break;

            // ~5%: currently waiting on another internal team.
            case < 70:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.PendingInternal, owner);
                break;

            // ~5%: unblocked by the internal team and back in progress.
            case < 75:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.PendingInternal, owner);
                Move(TicketStatus.InProgress, owner);
                break;

            // ~12%: resolved but not yet closed.
            case < 87:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Move(TicketStatus.PendingCustomer, owner);
                Move(TicketStatus.Resolved, owner);
                break;

            // ~8%: fully closed, with the reason the close transition requires.
            case < 95:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Move(TicketStatus.Resolved, owner);
                Move(TicketStatus.Closed, DemoTicketActor.Manager);
                break;

            // ~3%: escalated to a department, then reassigned and worked.
            case < 98:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Escalate(TicketEscalationTargetType.Department, null, DemoTicketActor.Manager);
                Assign(other, DemoTicketActor.Manager, replacingOwner: true);
                Move(TicketStatus.PendingInternal, other);
                break;

            // ~2%: escalated twice — level 1 to an agent, level 2 to a department.
            default:
                Assign(owner, DemoTicketActor.Manager, replacingOwner: false);
                Move(TicketStatus.InProgress, owner);
                Escalate(TicketEscalationTargetType.Agent, DemoTicketActor.Manager, owner);
                Escalate(TicketEscalationTargetType.Department, null, DemoTicketActor.Manager);
                Assign(other, DemoTicketActor.Manager, replacingOwner: true);
                break;
        }

        return new DemoTicketPlan(ageInDays, createdHour, steps);
    }

    private static string ReasonFor(TicketStatus current, TicketStatus target) => (current, target) switch
    {
        (_, TicketStatus.Closed) => CloseReason,
        (TicketStatus.Resolved or TicketStatus.Closed, _) => "Reopened at the customer's request.",
        (_, TicketStatus.InProgress) => ProgressReason,
        (_, TicketStatus.PendingCustomer) => WaitingCustomerReason,
        (_, TicketStatus.PendingInternal) => WaitingInternalReason,
        (_, TicketStatus.Resolved) => ResolveReason,
        _ => "Demo status change.",
    };
}
