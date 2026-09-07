namespace SquadCrm.Modules.CustomerManagement.Persistence;

/// <summary>
/// Provider-neutral pointer to a customer attachment (CRM-128). File bytes
/// live in the shared <c>IFileStorage</c>; this row holds display metadata.
/// Removal is logical (<see cref="RemovedAtUtc"/>) because audit/historical
/// requirements may prevent physical deletion.
/// </summary>
public sealed class CustomerAttachment
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public required string StorageKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public string? Description { get; set; }
    public required string UploadedBy { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DateTimeOffset? RemovedAtUtc { get; set; }
    public string? RemovedBy { get; set; }
}
