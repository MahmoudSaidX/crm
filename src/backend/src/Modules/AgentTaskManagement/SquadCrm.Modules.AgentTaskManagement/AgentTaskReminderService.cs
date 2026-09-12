using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SquadCrm.Modules.AgentTaskManagement.Persistence;

namespace SquadCrm.Modules.AgentTaskManagement;

/// <summary>
/// Fires reminders that have fallen due (CRM-144). A periodic SWEEP over
/// module-owned state rather than one Hangfire job per reminder: a
/// per-reminder job id would put business identity into Hangfire, and every
/// reschedule/cancel would then have to delete a Hangfire job to stay
/// correct. Here Hangfire only decides "run now"; what is due, what already
/// fired and what was superseded is answered entirely by
/// <see cref="AgentTask"/> (CLAUDE.md: Hangfire is execution infrastructure,
/// not business state).
/// <para>
/// Runs outside any HTTP request, so it deliberately never touches
/// <c>ICurrentUserAccessor</c>; it also performs no authorization, because it
/// only writes an event addressed to the task's own owner. Any deep link that
/// event later produces is re-authorized by the module that owns the target
/// (BR: a reminder grants no access).
/// </para>
/// </summary>
public sealed class AgentTaskReminderService(
    AgentTaskManagementDbContext dbContext,
    ILogger<AgentTaskReminderService> logger)
{
    /// <summary>
    /// Bounded so one sweep cannot turn into an unbounded write burst after a
    /// long outage; the job runs every minute, so a backlog drains quickly.
    /// </summary>
    internal const int BatchSize = 200;

    /// <summary>
    /// Transitions every due reminder to <c>Triggered</c> and writes its
    /// durable reminder event to this module's outbox in ONE transaction, so
    /// a crash mid-sweep can neither lose an event nor mark a reminder fired
    /// without one.
    /// <para>
    /// Idempotent on retry by construction: the outbox row's primary key is
    /// the reminder's own <c>ReminderEventId</c>, and a reminder already
    /// moved out of <c>Scheduled</c> is no longer selected here at all.
    /// </para>
    /// </summary>
    /// <returns>The number of reminders fired.</returns>
    public async Task<int> TriggerDueRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        List<AgentTask> due = await dbContext.AgentTasks
            .Where(task =>
                task.ReminderStatus == AgentTaskReminderStatus.Scheduled
                && task.ReminderAtUtc != null
                && task.ReminderAtUtc <= now
                // AC: completed (or otherwise not-open) tasks generate no
                // reminders. Completion already cancels a scheduled reminder;
                // this is the belt-and-braces read-side guarantee.
                && task.Status == AgentTaskStatus.Open)
            .OrderBy(task => task.ReminderAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (AgentTask task in due)
        {
            task.TriggerReminder(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Task titles can carry customer detail — log the count only, never
        // the reminder payload.
        logger.LogInformation("Triggered {DueReminderCount} due agent task reminder(s).", due.Count);
        return due.Count;
    }
}
