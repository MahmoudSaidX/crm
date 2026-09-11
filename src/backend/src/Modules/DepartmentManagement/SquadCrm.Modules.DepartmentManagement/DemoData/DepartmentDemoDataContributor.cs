using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.DepartmentManagement.Persistence;

namespace SquadCrm.Modules.DepartmentManagement.DemoData;

/// <summary>
/// DepartmentManagement's own demo-data contributor. Idempotent on
/// <see cref="Department.NormalizedCode"/>; only the fields this module's model
/// actually has are written.
/// </summary>
public sealed class DepartmentDemoDataContributor : IDemoDataContributor
{
    /// <summary>
    /// Five active demo departments plus one inactive row, which exists so
    /// active/inactive filters and history views have something to show.
    /// </summary>
    internal static readonly (string Code, string ArabicName, string EnglishName, bool IsActive)[] Departments =
    [
        ("customer-support", "دعم العملاء", "Customer Support", true),
        ("technical-support", "الدعم الفني", "Technical Support", true),
        ("billing", "الفوترة", "Billing", true),
        ("operations", "العمليات", "Operations", true),
        ("customer-success", "نجاح العملاء", "Customer Success", true),
        ("legacy-collections", "التحصيل (سابقًا)", "Collections (retired)", false),
    ];

    public string Name => "DepartmentManagement";

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using DepartmentManagementDbContext dbContext =
            new DepartmentManagementDbContextFactory().CreateDbContext([]);

        int created = 0;
        foreach ((string code, string arabicName, string englishName, bool isActive) in Departments)
        {
            string normalizedCode = code.Trim().ToUpperInvariant();
            Department? department = await dbContext.Departments.SingleOrDefaultAsync(
                candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);

            if (department is null)
            {
                department = new Department
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    NormalizedCode = normalizedCode,
                    ArabicName = arabicName,
                    EnglishName = englishName,
                    Description = "DEMO DATA — development demo department.",
                    IsActive = isActive,
                    CreatedAtUtc = scope.NowUtc,
                    UpdatedAtUtc = scope.NowUtc,
                };
                dbContext.Departments.Add(department);
                created++;
            }

            if (isActive && department.IsActive)
            {
                scope.References.Departments.Add(new DemoCatalogReference(code, department.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["departments created"] = created,
                ["departments total"] = Departments.Length,
                ["active departments published"] = scope.References.Departments.Count,
            });
    }
}
