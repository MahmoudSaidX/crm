using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

internal sealed class CustomerExistsLookup(CustomerManagementDbContext dbContext) : ICustomerExistsLookup
{
    public async Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken) =>
        await dbContext.Customers.AsNoTracking()
            .AnyAsync(customer => customer.Id == customerId, cancellationToken);
}
