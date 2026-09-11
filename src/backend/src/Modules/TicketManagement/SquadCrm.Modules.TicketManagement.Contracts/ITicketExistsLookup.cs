namespace SquadCrm.Modules.TicketManagement.Contracts;

/// <summary>
/// Existence-only check for a ticket id (CRM-143). Mirrors
/// <c>ICustomerExistsLookup</c> exactly. Deliberately narrow: it answers only
/// "does this ticket exist", never anything about access to it — linking
/// another module's record to a ticket by id must never grant access to that
/// ticket (Business Rule).
/// </summary>
public interface ITicketExistsLookup
{
    Task<bool> ExistsAsync(Guid ticketId, CancellationToken cancellationToken);
}
