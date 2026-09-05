namespace SquadCrm.Modules.CustomerManagement.Persistence;

public enum CustomerPreferredLanguage
{
    Arabic,
    English,
}

public enum CustomerStatus
{
    Active,
    Inactive,
}

/// <summary>
/// Customer identity/profile record. Contact details, notes, attachments and
/// interaction history are added by later stories (CRM-126/127/128/129), not
/// here.
/// </summary>
public sealed class Customer
{
    public Guid Id { get; set; }
    public required string CustomerNumber { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string NormalizedFirstName { get; set; }
    public required string NormalizedLastName { get; set; }
    public CustomerPreferredLanguage? PreferredLanguage { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Non-nullable mirror of <see cref="DepartmentId"/> (<see cref="Guid.Empty"/>
    /// when unset), used only by the duplicate-match unique index: Postgres
    /// treats every NULL as distinct, so a nullable column cannot back a
    /// reliable uniqueness guarantee.
    /// </summary>
    public Guid DepartmentMatchId { get; set; }

    /// <summary>Non-nullable mirror of <see cref="BranchId"/>; see <see cref="DepartmentMatchId"/>.</summary>
    public Guid BranchMatchId { get; set; }
    public CustomerStatus Status { get; set; } = CustomerStatus.Active;

    /// <summary>
    /// Optimistic concurrency token mapped to Postgres's native <c>xmin</c>
    /// system column — no extra column/migration needed, and (unlike a
    /// shadow property) readable from plain <c>AsNoTracking</c> queries.
    /// </summary>
    public uint Version { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
