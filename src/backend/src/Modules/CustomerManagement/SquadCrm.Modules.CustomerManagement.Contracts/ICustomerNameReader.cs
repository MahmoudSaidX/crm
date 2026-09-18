namespace SquadCrm.Modules.CustomerManagement.Contracts;

/// <summary>Denormalized display name for a customer a caller already has access to (CRM-146).</summary>
public sealed record CustomerName(string FirstName, string LastName);

/// <summary>
/// Read-only display lookup, deliberately narrow like <see cref="ICustomerExistsLookup"/>:
/// returns the customer's own name fields only and carries no authorization
/// opinion of its own.
/// </summary>
public interface ICustomerNameReader
{
    Task<CustomerName?> GetAsync(Guid customerId, CancellationToken cancellationToken);
}
