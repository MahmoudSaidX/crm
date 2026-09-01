namespace SquadCrm.Modules.Audit.Persistence;

/// <summary>
/// Append-only audit row. No <c>Update</c>/<c>Delete</c> path exists anywhere in
/// the codebase or its API — mutation only ever happens through
/// <c>DbSet.Add</c> from <see cref="AuditRecorder"/>.
/// </summary>
public sealed class AuditRecord
{
    public long Id { get; set; }
    public required string ActorHandle { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
