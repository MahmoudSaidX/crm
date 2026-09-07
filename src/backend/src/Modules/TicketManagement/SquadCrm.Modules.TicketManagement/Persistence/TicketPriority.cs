namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// Ticket urgency catalog row (CRM-132, second story of the Ticket
/// Management epic CRM-130). There is no Ticket entity yet — a later story
/// consumes this catalog by <see cref="Id"/> for selection/SLA/automation;
/// that consumption is not built here.
/// </summary>
public sealed class TicketPriority
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public required string ArabicName { get; set; }
    public required string EnglishName { get; set; }
    public int Rank { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
