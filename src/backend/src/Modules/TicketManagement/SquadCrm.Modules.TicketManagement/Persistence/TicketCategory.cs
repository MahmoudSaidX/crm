namespace SquadCrm.Modules.TicketManagement.Persistence;

/// <summary>
/// Ticket classification catalog row (CRM-131, first story of the Ticket
/// Management epic CRM-130). There is no Ticket entity yet — a later story
/// consumes this catalog by <see cref="Id"/> for selection/routing; that
/// consumption is not built here.
/// </summary>
public sealed class TicketCategory
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string NormalizedCode { get; set; }
    public required string ArabicName { get; set; }
    public required string EnglishName { get; set; }
    public Guid? DefaultDepartmentId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
