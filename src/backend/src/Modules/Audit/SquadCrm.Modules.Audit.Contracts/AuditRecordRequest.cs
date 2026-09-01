namespace SquadCrm.Modules.Audit.Contracts;

public sealed record AuditRecordRequest(
    string ActorHandle,
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, string>? Metadata = null);

public interface IAuditRecorder
{
    Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken);
}
