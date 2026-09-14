namespace SquadCrm.Modules.CustomerManagement.Domain.Entities;

/// <summary>
/// An internal-only note attached to a customer (CRM-127). Notes are
/// immutable once created — no update/delete path exists.
/// </summary>
public sealed class CustomerNote
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public required string Body { get; set; }
    public Guid AuthorUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}
