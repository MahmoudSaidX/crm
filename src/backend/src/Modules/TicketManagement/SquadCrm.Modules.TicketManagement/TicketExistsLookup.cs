using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.TicketManagement.Contracts;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement;

internal sealed class TicketExistsLookup(TicketManagementDbContext dbContext) : ITicketExistsLookup
{
    public async Task<bool> ExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await dbContext.Tickets.AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);
}
