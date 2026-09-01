using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.BranchManagement;

public sealed record CreateBranchRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: MaxLength(1000)] string? Description);

public sealed record UpdateBranchRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: MaxLength(1000)] string? Description);

public sealed record BranchResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
