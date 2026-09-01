using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.DepartmentManagement;

public sealed record CreateDepartmentRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: MaxLength(1000)] string? Description);

public sealed record UpdateDepartmentRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: MaxLength(1000)] string? Description);

public sealed record DepartmentResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
