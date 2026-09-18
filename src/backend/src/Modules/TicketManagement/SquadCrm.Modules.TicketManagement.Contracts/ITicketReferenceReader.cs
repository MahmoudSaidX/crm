namespace SquadCrm.Modules.TicketManagement.Contracts;

/// <summary>Denormalized display fields for a ticket a caller already has access to (CRM-146).</summary>
public sealed record TicketReference(string TicketNumber, Guid CustomerId);

/// <summary>
/// Read-only display lookup, deliberately narrow like <see cref="ITicketExistsLookup"/>:
/// it returns the ticket's own reference fields only, and carries no
/// authorization opinion — the caller (e.g. QuickReplyManagement's variable
/// resolver) is responsible for checking the caller may view tickets before
/// using this.
/// </summary>
public interface ITicketReferenceReader
{
    Task<TicketReference?> GetAsync(Guid ticketId, CancellationToken cancellationToken);
}
