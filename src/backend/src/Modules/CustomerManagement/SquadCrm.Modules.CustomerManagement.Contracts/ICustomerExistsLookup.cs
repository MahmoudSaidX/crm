namespace SquadCrm.Modules.CustomerManagement.Contracts;

public interface ICustomerExistsLookup
{
    Task<bool> ExistsAsync(Guid customerId, CancellationToken cancellationToken);
}
