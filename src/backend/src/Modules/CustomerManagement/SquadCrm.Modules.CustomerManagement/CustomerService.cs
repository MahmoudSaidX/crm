using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
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

    public async Task<PagedResult<Customer>> ListAsync(
        CustomerListQuery query,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        IQueryable<Customer> filtered = dbContext.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string normalizedSearch = Normalize(query.Search);
            filtered = filtered.Where(customer =>
                customer.CustomerNumber.Contains(normalizedSearch)
                || customer.NormalizedFirstName.Contains(normalizedSearch)
                || customer.NormalizedLastName.Contains(normalizedSearch));
        }

        if (query.DepartmentIds is { Length: > 0 })
        {
            filtered = filtered.Where(customer =>
                customer.DepartmentId != null && query.DepartmentIds.Contains(customer.DepartmentId.Value));
        }

        if (query.BranchIds is { Length: > 0 })
        {
            filtered = filtered.Where(customer =>
                customer.BranchId != null && query.BranchIds.Contains(customer.BranchId.Value));
        }

        if (query.Status is { Length: > 0 })
        {
            filtered = filtered.Where(customer => query.Status.Contains(customer.Status));
        }

        // Every branch orders by CustomerNumber (unique) as a stable
        // tiebreaker after the requested sort key, so paginated results
        // never reorder across pages regardless of SortBy/SortDirection.
        IOrderedQueryable<Customer> sorted = (query.SortBy, query.SortDirection) switch
        {
            (CustomerSortBy.FirstName, SortDirection.Desc) => filtered.OrderByDescending(c => c.FirstName),
            (CustomerSortBy.FirstName, _) => filtered.OrderBy(c => c.FirstName),
            (CustomerSortBy.LastName, SortDirection.Desc) => filtered.OrderByDescending(c => c.LastName),
            (CustomerSortBy.LastName, _) => filtered.OrderBy(c => c.LastName),
            (CustomerSortBy.CreatedAtUtc, SortDirection.Desc) => filtered.OrderByDescending(c => c.CreatedAtUtc),
            (CustomerSortBy.CreatedAtUtc, _) => filtered.OrderBy(c => c.CreatedAtUtc),
            (_, SortDirection.Desc) => filtered.OrderByDescending(c => c.CustomerNumber),
            _ => filtered.OrderBy(c => c.CustomerNumber),
        };
        IOrderedQueryable<Customer> ordered = sorted.ThenBy(c => c.CustomerNumber);

        int totalCount = await ordered.CountAsync(cancellationToken);
        List<Customer> items = await ordered
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<Customer>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public async Task<Customer?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Customers.AsNoTracking().SingleOrDefaultAsync(customer => customer.Id == id, cancellationToken);

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
