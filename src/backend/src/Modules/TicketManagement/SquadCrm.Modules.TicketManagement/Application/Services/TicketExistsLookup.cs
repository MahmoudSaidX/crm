using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.TicketManagement.Contracts;
using SquadCrm.Modules.TicketManagement.Infrastructure.Persistence;

namespace SquadCrm.Modules.TicketManagement.Application.Services;

internal sealed class TicketExistsLookup(TicketManagementDbContext dbContext) : ITicketExistsLookup
{
    public async Task<bool> ExistsAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await dbContext.Tickets.AsNoTracking()
            .AnyAsync(ticket => ticket.Id == ticketId, cancellationToken);
}
