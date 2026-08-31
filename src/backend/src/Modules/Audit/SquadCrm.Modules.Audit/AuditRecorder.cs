using Microsoft.Extensions.Logging;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;

namespace SquadCrm.Modules.Audit;

/// <summary>
/// The one and only best-effort audit trail in the codebase (see Story
/// CRM-114's transaction-boundary note): <see cref="AuditDbContext"/> is a
/// separate <c>DbContext</c>/connection from the caller's own, so this write
/// cannot participate in the caller's transaction without leaking the
/// caller's persistence lifetime into this cross-module contract. The caller
/// always calls this only after its own <c>SaveChangesAsync</c> has already
/// succeeded; a failure here is caught and logged, never rethrown — it must
/// never fail or roll back the business operation it is recording. Do not
/// "fix" this into a distributed/cross-DbContext transaction.
/// </summary>
public sealed class AuditRecorder(AuditDbContext dbContext, ILogger<AuditRecorder> logger) : IAuditRecorder
{
    public async Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken)
    {
        try
        {
            dbContext.AuditRecords.Add(new AuditRecord
            {
                ActorHandle = request.ActorHandle,
                Action = request.Action,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                MetadataJson = request.Metadata is { Count: > 0 }
                    ? System.Text.Json.JsonSerializer.Serialize(request.Metadata)
                    : null,
                OccurredAtUtc = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to record audit entry for {Action} on {EntityType}/{EntityId}.",
                request.Action, request.EntityType, request.EntityId);
        }
    }
}
