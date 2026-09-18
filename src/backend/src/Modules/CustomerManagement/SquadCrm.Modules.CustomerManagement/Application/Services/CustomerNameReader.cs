using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Infrastructure.Persistence;

namespace SquadCrm.Modules.CustomerManagement.Application.Services;

internal sealed class CustomerNameReader(CustomerManagementDbContext dbContext) : ICustomerNameReader
{
    public Task<CustomerName?> GetAsync(Guid customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AsNoTracking()
            .Where(customer => customer.Id == customerId)
            .Select(customer => new CustomerName(customer.FirstName, customer.LastName))
            .SingleOrDefaultAsync(cancellationToken);
}
