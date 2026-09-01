namespace SquadCrm.Modules.DepartmentManagement.Persistence;

/// <summary>
/// Organizational structure reference (not a security role). Other modules —
/// staff memberships (CRM-111), ticket/customer scope — will reference this
/// catalog row by <see cref="Id"/>; that consumption is built in those
/// stories, not here.
/// </summary>
public sealed class Department
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public required string ArabicName { get; set; }
    public required string EnglishName { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
