namespace SquadCrm.Modules.CustomerManagement.Persistence;

public enum CustomerContactType
{
    Email,
    Phone,
}

/// <summary>
/// A customer's email/phone contact channel (CRM-126). Verification workflow
/// and communication-snapshot integration are added by later stories, not
/// here.
/// </summary>
public sealed class CustomerContact
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerContactType Type { get; set; }
    public required string Value { get; set; }
    public required string NormalizedValue { get; set; }
    public string? Label { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? VerifiedAtUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
