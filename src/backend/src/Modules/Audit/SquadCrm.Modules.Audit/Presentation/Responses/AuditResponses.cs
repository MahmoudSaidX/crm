namespace SquadCrm.Modules.Audit.Presentation.Responses;

public sealed record AuditRecordResponse(
    long Id,
    string ActorHandle,
    string Action,
    string EntityType,
    string EntityId,
    IReadOnlyDictionary<string, string>? Metadata,
    DateTimeOffset OccurredAtUtc);
