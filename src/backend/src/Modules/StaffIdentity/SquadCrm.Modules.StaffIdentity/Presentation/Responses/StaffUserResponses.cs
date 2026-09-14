namespace SquadCrm.Modules.StaffIdentity.Presentation.Responses;

public sealed record StaffUserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    string? Department,
    string? Branch,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
