namespace SquadCrm.Modules.BranchManagement.Presentation.Responses;

public sealed record BranchResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
