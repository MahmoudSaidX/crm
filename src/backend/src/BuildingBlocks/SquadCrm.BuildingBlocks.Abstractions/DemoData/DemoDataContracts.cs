namespace SquadCrm.BuildingBlocks.Abstractions.DemoData;

/// <summary>
/// Dataset size a demo-data run produces. The concrete row counts live with the
/// seeder tool, not here: a module contributor only needs to know which of the
/// three shapes it is producing.
/// </summary>
public enum DemoDataSize
{
    Small,
    Medium,
    Large,
}

/// <summary>A demo staff account published by the StaffIdentity contributor.</summary>
public sealed record DemoStaffReference(string Email, Guid Id, string DisplayName, string RoleCode);

/// <summary>A demo catalog row (department, branch, ticket category, ticket priority) published by its owning module.</summary>
public sealed record DemoCatalogReference(string Code, Guid Id);

/// <summary>A demo customer published by the CustomerManagement contributor.</summary>
public sealed record DemoCustomerReference(string CustomerNumber, Guid Id, Guid DepartmentId, Guid BranchId);

/// <summary>
/// References each module publishes for the contributors that run after it.
/// <para>
/// This exists so no contributor ever reads another module's tables: a module
/// that needs a department id or a staff subject id consumes the reference the
/// owning module published, exactly as it would consume a cross-module contract
/// at runtime.
/// </para>
/// </summary>
public sealed class DemoDataReferences
{
    public List<DemoStaffReference> Staff { get; } = [];
    public List<DemoCatalogReference> Departments { get; } = [];
    public List<DemoCatalogReference> Branches { get; } = [];
    public List<DemoCatalogReference> TicketCategories { get; } = [];
    public List<DemoCatalogReference> TicketPriorities { get; } = [];
    public List<DemoCustomerReference> Customers { get; } = [];

    /// <summary>The demo staff account with this email, or throws when the StaffIdentity contributor did not publish it.</summary>
    public DemoStaffReference RequireStaff(string email) =>
        Staff.Single(candidate => string.Equals(candidate.Email, email, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Everything a contributor needs for one demo-data run. <paramref name="RandomSeed"/>
/// is fixed by the caller so a run against a clean database is reproducible;
/// <paramref name="NowUtc"/> is captured once so every contributor places its
/// historical window relative to the same instant.
/// </summary>
public sealed record DemoDataScope(
    DemoDataSize Size,
    int RandomSeed,
    DateTimeOffset NowUtc,
    DemoDataReferences References);

/// <summary>What one contributor created, for the run summary. Never carries secrets.</summary>
public sealed record DemoDataOutcome(string Contributor, IReadOnlyDictionary<string, int> Created);

/// <summary>
/// A module's own demo-data contributor. Each implementation lives in the module
/// that owns the data, writes only through that module's persistence and domain
/// methods, and is idempotent on a deterministic business key. The seeder tool
/// only orchestrates implementations in dependency order.
/// </summary>
public interface IDemoDataContributor
{
    /// <summary>Contributor name, used in the run summary only.</summary>
    string Name { get; }

    Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken);
}
