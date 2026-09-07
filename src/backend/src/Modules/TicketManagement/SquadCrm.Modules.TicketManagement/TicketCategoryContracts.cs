using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.TicketManagement;

public sealed record CreateTicketCategoryRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    Guid? DefaultDepartmentId,
    int SortOrder);

public sealed record UpdateTicketCategoryRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    Guid? DefaultDepartmentId,
    int SortOrder);

public sealed record TicketCategoryResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    Guid? DefaultDepartmentId,
    int SortOrder,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
