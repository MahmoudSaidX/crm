using Hangfire;
using SquadCrm.Modules.AgentTaskManagement.BackgroundProcessing;

namespace SquadCrm.Api;

/// <summary>
/// Registers the due-reminder sweep (CRM-144). Minutely is the useful floor
/// for a user-facing reminder: a reminder is late by at most one sweep, and
/// the sweep's query is an index seek over scheduled reminders only.
/// </summary>
internal sealed class AgentTaskReminderRecurringJobRegistration(IRecurringJobManager recurringJobs)
    : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        recurringJobs.AddOrUpdate<AgentTaskReminderJob>(
            "agent-task-reminder-dispatch",
            job => job.RunAsync(CancellationToken.None),
            Cron.Minutely);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
