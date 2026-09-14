using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.TicketManagement.Presentation.Requests;

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
