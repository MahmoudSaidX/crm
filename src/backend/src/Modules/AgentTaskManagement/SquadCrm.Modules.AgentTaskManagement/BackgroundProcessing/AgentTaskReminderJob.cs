namespace SquadCrm.Modules.AgentTaskManagement.BackgroundProcessing;

/// <summary>
/// Thin Hangfire entry point for the due-reminder sweep (CRM-144), mirroring
/// <c>ArchitectureFixtureOutboxJob</c>'s shape: the job holds no state and
/// makes no decisions — all durable reminder state stays module-owned.
/// </summary>
public sealed class AgentTaskReminderJob(AgentTaskReminderService reminderService)
{
    public Task RunAsync(CancellationToken cancellationToken) =>
        reminderService.TriggerDueRemindersAsync(DateTimeOffset.UtcNow, cancellationToken);
}
