using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

internal sealed class OutboxReadinessHealthCheck(
    ArchitectureFixtureDbContext dbContext,
    IOptions<OutboxProcessingOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            int retryCeiling = options.Value.RetryCeiling;
            int pending = await dbContext.OutboxMessages.CountAsync(
                message => message.ProcessedAtUtc == null && message.RetryCount < retryCeiling,
                cancellationToken);
            int failed = await dbContext.OutboxMessages.CountAsync(
                message => message.ProcessedAtUtc == null
                    && message.RetryCount > 0
                    && message.RetryCount < retryCeiling,
                cancellationToken);
            int exhausted = await dbContext.OutboxMessages.CountAsync(
                message => message.ProcessedAtUtc == null && message.RetryCount >= retryCeiling,
                cancellationToken);

            Dictionary<string, object> data = new()
            {
                ["pending"] = pending,
                ["failed"] = failed,
                ["exhausted"] = exhausted,
            };

            return exhausted > 0
                ? HealthCheckResult.Degraded("Outbox contains exhausted messages.", data: data)
                : HealthCheckResult.Healthy(data: data);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Outbox status is unavailable.");
        }
    }
}
