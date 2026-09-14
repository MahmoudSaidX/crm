namespace SquadCrm.Modules.TicketManagement.Presentation.Responses;

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
