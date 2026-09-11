using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.BranchManagement.Persistence;

namespace SquadCrm.Modules.BranchManagement.DemoData;

/// <summary>
/// BranchManagement's own demo-data contributor. Idempotent on
/// <see cref="Branch.NormalizedCode"/>. The model has no geographic fields, so
/// the city lives in the branch names only — nothing is invented.
/// </summary>
public sealed class BranchDemoDataContributor : IDemoDataContributor
{
    /// <summary>Eight active demo branches plus one inactive row for filter/history testing.</summary>
    internal static readonly (string Code, string ArabicName, string EnglishName, bool IsActive)[] Branches =
    [
        ("riyadh-hq", "الرياض — المركز الرئيسي", "Riyadh — Head Office", true),
        ("riyadh-olaya", "الرياض — العليا", "Riyadh — Olaya", true),
        ("jeddah", "جدة", "Jeddah", true),
        ("dammam", "الدمام", "Dammam", true),
        ("khobar", "الخبر", "Khobar", true),
        ("makkah", "مكة المكرمة", "Makkah", true),
        ("madinah", "المدينة المنورة", "Madinah", true),
        ("abha", "أبها", "Abha", true),
        ("taif-legacy", "الطائف (مغلق)", "Taif (closed)", false),
    ];

    public string Name => "BranchManagement";

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using BranchManagementDbContext dbContext = new BranchManagementDbContextFactory().CreateDbContext([]);

        int created = 0;
        foreach ((string code, string arabicName, string englishName, bool isActive) in Branches)
        {
            string normalizedCode = code.Trim().ToUpperInvariant();
            Branch? branch = await dbContext.Branches.SingleOrDefaultAsync(
                candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);

            if (branch is null)
            {
                branch = new Branch
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    NormalizedCode = normalizedCode,
                    ArabicName = arabicName,
                    EnglishName = englishName,
                    Description = "DEMO DATA — development demo branch.",
                    IsActive = isActive,
                    CreatedAtUtc = scope.NowUtc,
                    UpdatedAtUtc = scope.NowUtc,
                };
                dbContext.Branches.Add(branch);
                created++;
            }

            if (isActive && branch.IsActive)
            {
                scope.References.Branches.Add(new DemoCatalogReference(code, branch.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["branches created"] = created,
                ["branches total"] = Branches.Length,
                ["active branches published"] = scope.References.Branches.Count,
            });
    }
}
