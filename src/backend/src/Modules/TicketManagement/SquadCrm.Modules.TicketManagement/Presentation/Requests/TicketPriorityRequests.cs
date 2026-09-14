using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.TicketManagement.Presentation.Requests;

public sealed record CreateTicketPriorityRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: Range(1, int.MaxValue)] int Rank,
    [property: MaxLength(500)] string? Description);

public sealed record UpdateTicketPriorityRequest(
    [property: Required, MaxLength(64)] string Code,
    [property: Required, MaxLength(200)] string ArabicName,
    [property: Required, MaxLength(200)] string EnglishName,
    [property: Range(1, int.MaxValue)] int Rank,
    [property: MaxLength(500)] string? Description);
