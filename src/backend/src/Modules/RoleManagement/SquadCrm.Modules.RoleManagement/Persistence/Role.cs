namespace SquadCrm.Modules.RoleManagement.Persistence;

/// <summary>
/// Global role (no Branch/Department scope, per the intake's Business Rules).
/// Permissions are assigned to a role through CRM-113 (not this story); roles
/// are assigned to users through CRM-111 (not this story) — this entity is
/// only the first-class referenceable catalog row those stories will consume.
/// </summary>
public sealed class Role
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string NormalizedName { get; set; }
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
