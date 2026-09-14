namespace SquadCrm.Modules.DepartmentManagement.Presentation.Responses;

public sealed record DepartmentResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
