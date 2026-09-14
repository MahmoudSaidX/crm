namespace SquadCrm.Modules.TicketManagement.Presentation.Responses;

public sealed record TicketPriorityResponse(
    Guid Id,
    string Code,
    string ArabicName,
    string EnglishName,
    int Rank,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
