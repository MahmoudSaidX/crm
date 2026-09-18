using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.TicketManagement.Contracts;
using SquadCrm.Modules.TicketManagement.Infrastructure.Persistence;

namespace SquadCrm.Modules.TicketManagement.Application.Services;

internal sealed class TicketReferenceReader(TicketManagementDbContext dbContext) : ITicketReferenceReader
{
    public Task<TicketReference?> GetAsync(Guid ticketId, CancellationToken cancellationToken) =>
        dbContext.Tickets.AsNoTracking()
            .Where(ticket => ticket.Id == ticketId)
            .Select(ticket => new TicketReference(ticket.TicketNumber, ticket.CustomerId))
            .SingleOrDefaultAsync(cancellationToken);
}
