using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.CustomerManagement.DemoData;
using SquadCrm.Modules.TicketManagement.DemoData;
using SquadCrm.Modules.TicketManagement.Persistence;
using SquadCrm.Tools.DemoDataSeeder;

namespace SquadCrm.UnitTests;

/// <summary>
/// Rules the demo seeder must hold before it ever touches a database: it refuses
/// every environment but Development/Test, it only ever scripts transitions the
/// ticket domain allows, and it is deterministic.
/// </summary>
public sealed class DemoDataSeederTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Test")]
    public void EnvironmentGuard_AllowsDevelopmentAndTest(string environment) =>
        DemoDataEnvironmentGuard.EnsureNonProduction(environment);

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData("development")]
    [InlineData("DEVELOPMENT")]
    [InlineData("")]
    [InlineData(null)]
    public void EnvironmentGuard_RefusesEverythingElse(string? environment) =>
        Assert.Throws<InvalidOperationException>(() => DemoDataEnvironmentGuard.EnsureNonProduction(environment));

    [Theory]
    [InlineData(DemoDataSize.Small)]
    [InlineData(DemoDataSize.Medium)]
    [InlineData(DemoDataSize.Large)]
    public void TicketScripts_OnlyUseTransitionsTheDomainAllows(DemoDataSize size)
    {
        Random random = new(DemoSeedProgram.RandomSeed);

        for (int index = 0; index < TicketDemoDataContributor.CountFor(size); index++)
        {
            DemoTicketPlan plan = DemoTicketScript.For(index, random);
            TicketStatus status = TicketStatus.Open;
            int previousOffset = -1;

            foreach (DemoTicketStep step in plan.Steps)
            {
                Assert.True(step.HourOffset > previousOffset, $"Ticket {index}: steps must advance in time.");
                previousOffset = step.HourOffset;

                switch (step)
                {
                    case DemoTicketStep.StatusChange statusChange:
                        Assert.True(
                            TicketStatusTransitions.IsAllowed(status, statusChange.Target),
                            $"Ticket {index}: illegal transition {status} -> {statusChange.Target}.");
                        if (TicketStatusTransitions.RequiresReason(status, statusChange.Target))
                        {
                            Assert.False(string.IsNullOrWhiteSpace(statusChange.Reason));
                        }

                        status = statusChange.Target;
                        break;

                    case DemoTicketStep.Escalation escalation:
                        Assert.True(
                            status is not (TicketStatus.Resolved or TicketStatus.Closed),
                            $"Ticket {index}: escalated a {status} ticket.");
                        Assert.False(string.IsNullOrWhiteSpace(escalation.Reason));
                        break;
                }
            }
        }
    }

    [Fact]
    public void TicketScripts_AreDeterministicForTheSameSeed()
    {
        Random first = new(DemoSeedProgram.RandomSeed);
        Random second = new(DemoSeedProgram.RandomSeed);

        for (int index = 0; index < 400; index++)
        {
            DemoTicketPlan left = DemoTicketScript.For(index, first);
            DemoTicketPlan right = DemoTicketScript.For(index, second);

            // Compared step by step: DemoTicketPlan's generated equality compares
            // the Steps list by reference, which would pass vacuously.
            Assert.Equal(left.AgeInDays, right.AgeInDays);
            Assert.Equal(left.CreatedHour, right.CreatedHour);
            Assert.Equal(left.Steps, right.Steps);
        }
    }

    [Fact]
    public void TicketScripts_ProduceEveryLifecycleStatusAndBothAgents()
    {
        Random random = new(DemoSeedProgram.RandomSeed);
        HashSet<TicketStatus> finalStatuses = [];
        Dictionary<DemoTicketActor, int> assignedTo = [];
        int escalations = 0;
        int reassignments = 0;

        for (int index = 0; index < TicketDemoDataContributor.CountFor(DemoDataSize.Medium); index++)
        {
            DemoTicketPlan plan = DemoTicketScript.For(index, random);
            TicketStatus status = TicketStatus.Open;
            DemoTicketActor? owner = null;

            foreach (DemoTicketStep step in plan.Steps)
            {
                switch (step)
                {
                    case DemoTicketStep.StatusChange statusChange:
                        status = statusChange.Target;
                        break;
                    case DemoTicketStep.Assignment assignment:
                        if (owner is not null)
                        {
                            reassignments++;
                            Assert.False(string.IsNullOrWhiteSpace(assignment.Reason));
                        }

                        owner = assignment.Target;
                        break;
                    case DemoTicketStep.Escalation:
                        escalations++;
                        break;
                }
            }

            finalStatuses.Add(status);
            if (owner is not null)
            {
                assignedTo[owner.Value] = assignedTo.GetValueOrDefault(owner.Value) + 1;
            }
        }

        Assert.Equal(Enum.GetValues<TicketStatus>().ToHashSet(), finalStatuses);
        Assert.True(escalations > 0, "The medium dataset must contain escalations.");
        Assert.True(reassignments > 0, "The medium dataset must contain reassignments.");

        // /my-tickets is only useful when both demo agents own a real queue.
        Assert.True(assignedTo.GetValueOrDefault(DemoTicketActor.Agent1) >= 30);
        Assert.True(assignedTo.GetValueOrDefault(DemoTicketActor.Agent2) >= 30);
    }

    [Fact]
    public void CustomerNamePairs_AreUniqueForTheLargestDataset()
    {
        int target = CustomerDemoDataContributor.CountFor(DemoDataSize.Large);
        Assert.True(target <= DemoCustomerNames.Capacity);

        HashSet<(string First, string Last)> pairs = [];
        for (int index = 0; index < target; index++)
        {
            Assert.True(pairs.Add(DemoCustomerNames.At(index)), $"Duplicate name pair at index {index}.");
        }
    }
}
