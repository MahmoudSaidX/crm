namespace SquadCrm.Modules.RoleManagement.Presentation.Responses;

public sealed record RoleResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
