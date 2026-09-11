using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.DemoData;

/// <summary>
/// TicketManagement's own demo-data contributor: tickets and their assignment,
/// status and escalation history.
/// <para>
/// Every mutation goes through the canonical domain methods
/// (<see cref="Ticket.Create"/>, <see cref="Ticket.Assign"/>,
/// <see cref="Ticket.ChangeStatus"/>, <see cref="Ticket.Escalate"/>), so
/// <c>Version</c>, <c>UpdatedAtUtc</c> and the domain events behave exactly as
/// they do in the application — the outbox interceptor turns those events into
/// <c>outbox_message</c> rows in the same save. History rows are appended in the
/// same change tracker, exactly as <c>TicketService</c> does.
/// </para>
/// <para>
/// <c>TicketService</c> itself cannot be reused here: it is internal to this
/// module's service graph and stamps <c>DateTimeOffset.UtcNow</c>, so it cannot
/// produce the 90-day historical window this demo data needs.
/// </para>
/// </summary>
public sealed class TicketDemoDataContributor : IDemoDataContributor
{
    internal const string DemoTicketNumberPrefix = "DEMO-T";

    private const int SaveBatchSize = 200;

    private static readonly TicketChannel[] Channels =
    [
        TicketChannel.Agent,
        TicketChannel.Portal,
        TicketChannel.Email,
        TicketChannel.WhatsApp,
        TicketChannel.LiveChat,
        TicketChannel.WebForm,
    ];

    public string Name => "TicketManagement (tickets)";

    /// <summary>Ticket count for each dataset size.</summary>
    public static int CountFor(DemoDataSize size) => size switch
    {
        DemoDataSize.Small => 50,
        DemoDataSize.Large => 5000,
        _ => 400,
    };

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.References.Customers.Count == 0
            || scope.References.TicketCategories.Count == 0
            || scope.References.TicketPriorities.Count == 0)
        {
            throw new InvalidOperationException(
                "Demo customers, ticket categories and ticket priorities must be seeded before tickets.");
        }

        await using TicketManagementDbContext dbContext = new TicketManagementDbContextFactory().CreateDbContext([]);

        HashSet<string> existingNumbers = [.. await dbContext.Tickets
            .Where(ticket => ticket.TicketNumber.StartsWith(DemoTicketNumberPrefix))
            .Select(ticket => ticket.TicketNumber)
            .ToListAsync(cancellationToken)];

        Dictionary<DemoTicketActor, DemoStaffReference> actors = new()
        {
            [DemoTicketActor.Agent1] = scope.References.RequireStaff("agent1@squadcrm.local"),
            [DemoTicketActor.Agent2] = scope.References.RequireStaff("agent2@squadcrm.local"),
            [DemoTicketActor.Manager] = scope.References.RequireStaff("manager@squadcrm.local"),
            [DemoTicketActor.Admin] = scope.References.RequireStaff("admin@squadcrm.local"),
        };

        Dictionary<string, Guid> categoriesByCode = scope.References.TicketCategories
            .ToDictionary(category => category.Code, category => category.Id, StringComparer.Ordinal);
        Guid[] categoryIds = [.. scope.References.TicketCategories.Select(category => category.Id)];
        Guid[] priorityIds = [.. scope.References.TicketPriorities.Select(priority => priority.Id)];
        Guid[] departmentIds = [.. scope.References.Departments.Select(department => department.Id)];

        Random random = new(scope.RandomSeed);
        int target = CountFor(scope.Size);
        int ticketsCreated = 0;
        int assignmentRows = 0;
        int statusRows = 0;
        int escalationRows = 0;
        int pendingInBatch = 0;

        for (int index = 0; index < target; index++)
        {
            string ticketNumber = $"{DemoTicketNumberPrefix}{index + 1:D5}";

            // The plan is generated for every index, present or not, so the random
            // sequence — and therefore the data of later tickets — does not shift
            // when an existing ticket is skipped.
            DemoTicketPlan plan = DemoTicketScript.For(index, random);
            int priorityRoll = random.Next(0, priorityIds.Length);
            int channelRoll = random.Next(0, Channels.Length);
            int departmentRoll = random.Next(0, Math.Max(departmentIds.Length, 1));

            if (existingNumbers.Contains(ticketNumber))
            {
                continue;
            }

            DemoCustomerReference customer = scope.References.Customers[index % scope.References.Customers.Count];
            DemoTicketContent.Topic topic = DemoTicketContent.At(index);
            Guid categoryId = categoriesByCode.TryGetValue(topic.CategoryCode, out Guid mappedCategory)
                ? mappedCategory
                : categoryIds[index % categoryIds.Length];

            DateTimeOffset createdAtUtc = new DateTimeOffset(scope.NowUtc.UtcDateTime.Date, TimeSpan.Zero)
                .AddDays(-plan.AgeInDays)
                .AddHours(plan.CreatedHour);
            if (createdAtUtc > scope.NowUtc)
            {
                createdAtUtc = scope.NowUtc.AddHours(-1);
            }

            Ticket ticket = Ticket.Create(
                Guid.NewGuid(),
                ticketNumber,
                customer.Id,
                topic.Subject,
                topic.Description,
                categoryId,
                subcategoryId: null,
                priorityIds[priorityRoll],
                customer.DepartmentId,
                customer.BranchId,
                Channels[channelRoll],
                assignedAgentId: null,
                createdAtUtc);
            dbContext.Tickets.Add(ticket);
            ticketsCreated++;

            // Step offsets are compressed into the window between creation and
            // now, so a ticket created today can still carry a full history and
            // no history row is ever stamped in the future.
            double scale = StepScale(plan, createdAtUtc, scope.NowUtc);

            foreach (DemoTicketStep step in plan.Steps)
            {
                DateTimeOffset at = createdAtUtc.AddHours(step.HourOffset * scale);
                switch (step)
                {
                    case DemoTicketStep.Assignment assignment:
                        {
                            Guid? previousAgentId = ticket.AssignedAgentId;
                            DemoStaffReference targetAgent = actors[assignment.Target];
                            ticket.Assign(targetAgent.Id, assignment.Reason, TicketAssignmentSource.Manual, at);
                            dbContext.TicketAssignmentHistory.Add(new TicketAssignmentHistory
                            {
                                Id = Guid.NewGuid(),
                                TicketId = ticket.Id,
                                PreviousAgentId = previousAgentId,
                                NewAgentId = targetAgent.Id,
                                Reason = assignment.Reason,
                                Source = TicketAssignmentSource.Manual,
                                ChangedBy = actors[assignment.ActedBy].Email,
                                ChangedAtUtc = at,
                            });
                            assignmentRows++;
                            break;
                        }

                    case DemoTicketStep.StatusChange statusChange:
                        {
                            TicketStatus previousStatus = ticket.Status;
                            ticket.ChangeStatus(statusChange.Target, statusChange.Reason, at);
                            dbContext.TicketStatusHistory.Add(new TicketStatusHistory
                            {
                                Id = Guid.NewGuid(),
                                TicketId = ticket.Id,
                                PreviousStatus = previousStatus,
                                NewStatus = statusChange.Target,
                                Reason = statusChange.Reason,
                                ChangedBy = actors[statusChange.ActedBy].Email,
                                ChangedAtUtc = at,
                            });
                            statusRows++;
                            break;
                        }

                    case DemoTicketStep.Escalation escalation:
                        {
                            Guid targetId = escalation.TargetType == TicketEscalationTargetType.Agent
                                ? actors[escalation.AgentTarget ?? DemoTicketActor.Manager].Id
                                : departmentIds[departmentRoll % departmentIds.Length];
                            int previousLevel = ticket.EscalationLevel;
                            int newLevel = previousLevel + 1;
                            ticket.Escalate(
                                newLevel,
                                escalation.TargetType,
                                targetId,
                                escalation.Reason,
                                TicketEscalationSource.Manual,
                                at);
                            dbContext.TicketEscalationHistory.Add(new TicketEscalationHistory
                            {
                                Id = Guid.NewGuid(),
                                TicketId = ticket.Id,
                                PreviousLevel = previousLevel,
                                NewLevel = newLevel,
                                TargetType = escalation.TargetType,
                                TargetId = targetId,
                                Reason = escalation.Reason,
                                Source = TicketEscalationSource.Manual,
                                EscalatedBy = actors[escalation.ActedBy].Email,
                                EscalatedAtUtc = at,
                            });
                            escalationRows++;
                            break;
                        }
                }
            }

            pendingInBatch++;
            if (pendingInBatch >= SaveBatchSize)
            {
                // A batch boundary never splits a ticket from its history: the
                // save happens between tickets, never inside one.
                await dbContext.SaveChangesAsync(cancellationToken);
                pendingInBatch = 0;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["tickets created"] = ticketsCreated,
                ["tickets already present"] = target - ticketsCreated,
                ["assignment history rows"] = assignmentRows,
                ["status history rows"] = statusRows,
                ["escalation history rows"] = escalationRows,
            });
    }

    /// <summary>
    /// Factor applied to every step's hour offset so the whole scripted history
    /// fits between the ticket's creation and now, keeping the steps strictly
    /// ordered (distinct offsets stay distinct because no rounding happens).
    /// </summary>
    private static double StepScale(DemoTicketPlan plan, DateTimeOffset createdAtUtc, DateTimeOffset nowUtc)
    {
        int maxOffset = plan.Steps.Count == 0 ? 0 : plan.Steps.Max(step => step.HourOffset);
        if (maxOffset == 0)
        {
            return 1;
        }

        double availableHours = (nowUtc - createdAtUtc).TotalHours * 0.9;
        return availableHours >= maxOffset ? 1 : availableHours / maxOffset;
    }
}
