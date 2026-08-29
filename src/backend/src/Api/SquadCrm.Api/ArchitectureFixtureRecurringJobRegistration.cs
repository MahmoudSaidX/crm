using Hangfire;
using SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

namespace SquadCrm.Api;

/// <summary>Registers the single CRM-199 proving-ground recurring job.</summary>
internal sealed class ArchitectureFixtureRecurringJobRegistration(IRecurringJobManager recurringJobs)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<ArchitectureFixtureOutboxJob>(
            "architecture-fixture-outbox-delivery",
            job => job.RunAsync(CancellationToken.None),
            Cron.Minutely);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
