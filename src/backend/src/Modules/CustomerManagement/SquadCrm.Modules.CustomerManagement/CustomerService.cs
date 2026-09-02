using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Persistence;
using SquadCrm.Modules.DepartmentManagement.Contracts;

namespace SquadCrm.Modules.CustomerManagement;

/// <summary>Discriminates why a create call did not produce a <see cref="Customer"/>.</summary>
public enum CustomerMutationFailure
{
    None,
    DuplicateCustomer,
    InactiveDepartment,
    InactiveBranch,
}

public readonly record struct CustomerMutationResult(Customer? Customer, CustomerMutationFailure Failure)
{
    public static CustomerMutationResult Success(Customer customer) => new(customer, CustomerMutationFailure.None);
    public static CustomerMutationResult Failed(CustomerMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Postgres unique-violation SQLSTATE, used to translate a lost create race
/// (concurrent duplicate insert) into the same duplicate result the
/// pre-check produces, rather than letting a 500 leak through.
/// </summary>
internal sealed class CustomerService(
    CustomerManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder,
    IDepartmentActiveLookup departmentActiveLookup,
    IBranchActiveLookup branchActiveLookup)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<CustomerMutationResult> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (request.DepartmentId is Guid departmentId
            && !await departmentActiveLookup.IsActiveAsync(departmentId, cancellationToken))
        {
            return CustomerMutationResult.Failed(CustomerMutationFailure.InactiveDepartment);
        }

        if (request.BranchId is Guid branchId
            && !await branchActiveLookup.IsActiveAsync(branchId, cancellationToken))
        {
            return CustomerMutationResult.Failed(CustomerMutationFailure.InactiveBranch);
        }

        string normalizedFirstName = Normalize(request.FirstName);
        string normalizedLastName = Normalize(request.LastName);
        Guid departmentMatchId = request.DepartmentId ?? Guid.Empty;
        Guid branchMatchId = request.BranchId ?? Guid.Empty;
        if (await CheckDuplicateAsync(
            normalizedFirstName, normalizedLastName, departmentMatchId, branchMatchId, cancellationToken))
        {
            return CustomerMutationResult.Failed(CustomerMutationFailure.DuplicateCustomer);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        Customer customer = new()
        {
            Id = Guid.NewGuid(),
            CustomerNumber = GenerateCustomerNumber(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            NormalizedFirstName = normalizedFirstName,
            NormalizedLastName = normalizedLastName,
            PreferredLanguage = request.PreferredLanguage,
            DepartmentId = request.DepartmentId,
            BranchId = request.BranchId,
            DepartmentMatchId = departmentMatchId,
            BranchMatchId = branchMatchId,
            Status = CustomerStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.Customers.Add(customer);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return CustomerMutationResult.Failed(CustomerMutationFailure.DuplicateCustomer);
        }

        await RecordAuditAsync(customer.Id, "created", cancellationToken);
        return CustomerMutationResult.Success(customer);
    }

    private async Task<bool> CheckDuplicateAsync(
        string normalizedFirstName,
        string normalizedLastName,
        Guid departmentMatchId,
        Guid branchMatchId,
        CancellationToken cancellationToken) =>
        await dbContext.Customers.AnyAsync(
            customer => customer.NormalizedFirstName == normalizedFirstName
                && customer.NormalizedLastName == normalizedLastName
                && customer.DepartmentMatchId == departmentMatchId
                && customer.BranchMatchId == branchMatchId,
            cancellationToken);

    private Task RecordAuditAsync(Guid customerId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "Customer", customerId.ToString(), Metadata: null),
            cancellationToken);

    private static string GenerateCustomerNumber() => $"CUS-{Guid.NewGuid():N}"[..12].ToUpperInvariant();

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
