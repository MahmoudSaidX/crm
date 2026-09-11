using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Modules.TicketManagement.DemoData;

/// <summary>
/// TicketManagement's catalog demo-data contributor: ticket categories and
/// priorities. Kept separate from <see cref="TicketDemoDataContributor"/> so the
/// catalogs are in place (and published as references) before customers and
/// tickets are generated.
/// </summary>
public sealed class TicketCatalogDemoDataContributor : IDemoDataContributor
{
    /// <summary>
    /// Demo categories. <c>DefaultDepartmentCode</c> is resolved against the
    /// departments published by DepartmentManagement — never by reading that
    /// module's tables.
    /// </summary>
    internal static readonly (string Code, string ArabicName, string EnglishName, string? DefaultDepartmentCode,
        int SortOrder, bool IsActive)[] Categories =
    [
        ("account", "الحساب", "Account", "customer-support", 1, true),
        ("billing", "الفوترة", "Billing", "billing", 2, true),
        ("technical-issue", "مشكلة فنية", "Technical Issue", "technical-support", 3, true),
        ("delivery", "التوصيل", "Delivery", "operations", 4, true),
        ("service-request", "طلب خدمة", "Service Request", "customer-success", 5, true),
        ("complaint", "شكوى", "Complaint", "customer-support", 6, true),
        ("general-inquiry", "استفسار عام", "General Inquiry", null, 7, true),
        ("legacy-request", "طلب قديم", "Legacy Request", null, 8, false),
    ];

    /// <summary>
    /// Demo priorities. <c>Rank</c> follows the existing semantics — the catalog
    /// is ordered by ascending rank, so rank 1 is the most urgent.
    /// </summary>
    internal static readonly (string Code, string ArabicName, string EnglishName, int Rank, string Description)[]
        Priorities =
    [
        ("urgent", "عاجل", "Urgent", 1, "Service is unusable or a customer is blocked."),
        ("high", "مرتفع", "High", 2, "Significant impact with a workaround available."),
        ("normal", "عادي", "Normal", 3, "Standard support request."),
        ("low", "منخفض", "Low", 4, "Minor issue or general question."),
    ];

    public string Name => "TicketManagement (catalogs)";

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        await using TicketManagementDbContext dbContext = new TicketManagementDbContextFactory().CreateDbContext([]);

        Dictionary<string, Guid> departmentsByCode = scope.References.Departments
            .ToDictionary(department => department.Code, department => department.Id, StringComparer.Ordinal);

        int categoriesCreated = 0;
        foreach ((string code, string arabicName, string englishName, string? defaultDepartmentCode, int sortOrder,
            bool isActive) in Categories)
        {
            string normalizedCode = code.Trim().ToUpperInvariant();
            TicketCategory? category = await dbContext.TicketCategories.SingleOrDefaultAsync(
                candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);

            if (category is null)
            {
                Guid? defaultDepartmentId =
                    defaultDepartmentCode is not null
                    && departmentsByCode.TryGetValue(defaultDepartmentCode, out Guid departmentId)
                        ? departmentId
                        : null;

                category = new TicketCategory
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    NormalizedCode = normalizedCode,
                    ArabicName = arabicName,
                    EnglishName = englishName,
                    DefaultDepartmentId = defaultDepartmentId,
                    SortOrder = sortOrder,
                    IsActive = isActive,
                    CreatedAtUtc = scope.NowUtc,
                    UpdatedAtUtc = scope.NowUtc,
                };
                dbContext.TicketCategories.Add(category);
                categoriesCreated++;
            }

            if (isActive && category.IsActive)
            {
                scope.References.TicketCategories.Add(new DemoCatalogReference(code, category.Id));
            }
        }

        int prioritiesCreated = 0;
        foreach ((string code, string arabicName, string englishName, int rank, string description) in Priorities)
        {
            string normalizedCode = code.Trim().ToUpperInvariant();
            TicketPriority? priority = await dbContext.TicketPriorities.SingleOrDefaultAsync(
                candidate => candidate.NormalizedCode == normalizedCode, cancellationToken);

            if (priority is null)
            {
                priority = new TicketPriority
                {
                    Id = Guid.NewGuid(),
                    Code = code,
                    NormalizedCode = normalizedCode,
                    ArabicName = arabicName,
                    EnglishName = englishName,
                    Rank = rank,
                    Description = description,
                    IsActive = true,
                    CreatedAtUtc = scope.NowUtc,
                    UpdatedAtUtc = scope.NowUtc,
                };
                dbContext.TicketPriorities.Add(priority);
                prioritiesCreated++;
            }

            if (priority.IsActive)
            {
                scope.References.TicketPriorities.Add(new DemoCatalogReference(code, priority.Id));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["ticket categories created"] = categoriesCreated,
                ["ticket priorities created"] = prioritiesCreated,
                ["active categories published"] = scope.References.TicketCategories.Count,
                ["active priorities published"] = scope.References.TicketPriorities.Count,
            });
    }
}
