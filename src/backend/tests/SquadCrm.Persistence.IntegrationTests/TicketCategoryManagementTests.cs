using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.DepartmentManagement;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Persistence;
using SquadCrm.Modules.TicketManagement;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class TicketCategoryManagementTests
{
    public TicketCategoryManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_AndRecordsOneCreatedAuditEntry()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketCategoryService service = CreateService(context, auditRecorder, "agent@example.test");
        string code = UniqueCode();

        TicketCategoryMutationResult result = await service.CreateAsync(
            new CreateTicketCategoryRequest(code, "الفواتير", "Billing", null, 10), CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.None, result.Failure);
        Assert.NotNull(result.TicketCategory);
        Assert.True(result.TicketCategory!.IsActive);
        Assert.Equal(10, result.TicketCategory.SortOrder);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.TicketCategory.Id.ToString() && request.ActorHandle == "agent@example.test");
    }

    [Fact]
    public async Task Update_Succeeds_AndRecordsOneUpdatedAuditEntry()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketCategoryService service = CreateService(context, auditRecorder, "agent@example.test");
        TicketCategory category = await CreateCategoryAsync(service);

        TicketCategoryMutationResult result = await service.UpdateAsync(
            category.Id,
            new UpdateTicketCategoryRequest(category.Code, "الفواتير المحدثة", "Updated Billing", null, 20),
            CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.None, result.Failure);
        Assert.Equal("Updated Billing", result.TicketCategory!.EnglishName);
        Assert.Equal(20, result.TicketCategory.SortOrder);
        Assert.Contains(auditRecorder.Requests, request => request.Action == "updated" && request.EntityId == category.Id.ToString());
    }

    [Fact]
    public async Task Activate_And_Deactivate_EachRecordTheirOwnEvent_AndNeverDeleteTheRow()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketCategoryService service = CreateService(context, auditRecorder, "agent@example.test");
        TicketCategory category = await CreateCategoryAsync(service);

        TicketCategoryMutationResult deactivated = await service.DeactivateAsync(category.Id, CancellationToken.None);
        Assert.Equal(TicketCategoryMutationFailure.None, deactivated.Failure);
        Assert.False(deactivated.TicketCategory!.IsActive);

        TicketCategoryMutationResult activated = await service.ActivateAsync(category.Id, CancellationToken.None);
        Assert.Equal(TicketCategoryMutationFailure.None, activated.Failure);
        Assert.True(activated.TicketCategory!.IsActive);

        Assert.Contains(auditRecorder.Requests, request => request.Action == "deactivated" && request.EntityId == category.Id.ToString());
        Assert.Contains(auditRecorder.Requests, request => request.Action == "activated" && request.EntityId == category.Id.ToString());

        // Deactivating never deletes: the category remains readable/listable
        // (there is no Ticket entity yet to enforce selection against).
        TicketCategory? stillPresent = await service.GetAsync(category.Id, CancellationToken.None);
        Assert.NotNull(stillPresent);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateCode_SameOrDifferentCaseOrWhitespace_IsRejected(string variant)
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCategoryService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        string original = UniqueCode();
        TicketCategoryMutationResult firstResult = await service.CreateAsync(
            new CreateTicketCategoryRequest(original, "فئة", "Category", null, 0), CancellationToken.None);
        Assert.Equal(TicketCategoryMutationFailure.None, firstResult.Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        TicketCategoryMutationResult secondResult = await service.CreateAsync(
            new CreateTicketCategoryRequest(second, "فئة أخرى", "Another Category", null, 0), CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.DuplicateCode, secondResult.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using TicketManagementDbContext firstContext = PostgresTestDatabase.CreateTicketManagementContext();
        await using TicketManagementDbContext secondContext = PostgresTestDatabase.CreateTicketManagementContext();
        string code = UniqueCode();
        CreateTicketCategoryRequest request = new(code, "فئة", "Category", null, 0);

        Task<TicketCategoryMutationResult> first = CreateService(firstContext, new RecordingAuditRecorder(), "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<TicketCategoryMutationResult> second = CreateService(secondContext, new RecordingAuditRecorder(), "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        TicketCategoryMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == TicketCategoryMutationFailure.None);
        Assert.Single(results, result => result.Failure == TicketCategoryMutationFailure.DuplicateCode);
    }

    [Fact]
    public async Task UnknownId_OnUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCategoryService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid unknownId = Guid.NewGuid();

        Assert.Equal(TicketCategoryMutationFailure.NotFound,
            (await service.UpdateAsync(unknownId, new UpdateTicketCategoryRequest(UniqueCode(), "فئة", "Category", null, 0), CancellationToken.None)).Failure);
        Assert.Equal(TicketCategoryMutationFailure.NotFound, (await service.ActivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Equal(TicketCategoryMutationFailure.NotFound, (await service.DeactivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Null(await service.GetAsync(unknownId, CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithNullDefaultDepartment_Succeeds()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCategoryService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketCategoryMutationResult result = await service.CreateAsync(
            new CreateTicketCategoryRequest(UniqueCode(), "فئة", "Category", null, 0), CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.None, result.Failure);
        Assert.Null(result.TicketCategory!.DefaultDepartmentId);
    }

    [Fact]
    public async Task Create_WithActiveDefaultDepartment_Succeeds_AndStoresIt()
    {
        await using DepartmentManagementDbContext departmentContext = PostgresTestDatabase.CreateDepartmentManagementContext();
        DepartmentService departmentService = new(departmentContext, new StubCurrentUserAccessor("agent@example.test"), new RecordingAuditRecorder());
        DepartmentMutationResult department = await departmentService.CreateAsync(
            new CreateDepartmentRequest($"DEPT_{Guid.NewGuid():N}"[..20], "قسم", "Department", null), CancellationToken.None);
        Assert.Equal(DepartmentMutationFailure.None, department.Failure);

        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCategoryService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketCategoryMutationResult result = await service.CreateAsync(
            new CreateTicketCategoryRequest(UniqueCode(), "فئة", "Category", department.Department!.Id, 0), CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.None, result.Failure);
        Assert.Equal(department.Department.Id, result.TicketCategory!.DefaultDepartmentId);
    }

    [Fact]
    public async Task Create_WithInactiveDefaultDepartment_IsRejected()
    {
        await using DepartmentManagementDbContext departmentContext = PostgresTestDatabase.CreateDepartmentManagementContext();
        DepartmentService departmentService = new(departmentContext, new StubCurrentUserAccessor("agent@example.test"), new RecordingAuditRecorder());
        DepartmentMutationResult department = await departmentService.CreateAsync(
            new CreateDepartmentRequest($"DEPT_{Guid.NewGuid():N}"[..20], "قسم", "Department", null), CancellationToken.None);
        Assert.Equal(DepartmentMutationFailure.None, department.Failure);
        await departmentService.DeactivateAsync(department.Department!.Id, CancellationToken.None);

        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketCategoryService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        TicketCategoryMutationResult result = await service.CreateAsync(
            new CreateTicketCategoryRequest(UniqueCode(), "فئة", "Category", department.Department.Id, 0), CancellationToken.None);

        Assert.Equal(TicketCategoryMutationFailure.InactiveDepartment, result.Failure);
    }

    private static async Task<TicketCategory> CreateCategoryAsync(TicketCategoryService service)
    {
        TicketCategoryMutationResult result = await service.CreateAsync(
            new CreateTicketCategoryRequest(UniqueCode(), "فئة", "Category", null, 0), CancellationToken.None);
        return result.TicketCategory!;
    }

    private static TicketCategoryService CreateService(TicketManagementDbContext context, IAuditRecorder auditRecorder, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle), auditRecorder, new StubDepartmentActiveLookup());

    private static string UniqueCode() => $"CAT_{Guid.NewGuid():N}"[..20];

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class RecordingAuditRecorder : IAuditRecorder
    {
        public List<AuditRecordRequest> Requests { get; } = [];

        public Task RecordAsync(AuditRecordRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Real <see cref="IDepartmentActiveLookup"/> queries against the real
    /// department-management schema through a fresh context, mirroring the
    /// production DI wiring — used by tests that create their own department
    /// via <see cref="PostgresTestDatabase.CreateDepartmentManagementContext"/>
    /// so activation state is read from the same database.
    /// </summary>
    private sealed class StubDepartmentActiveLookup : IDepartmentActiveLookup
    {
        public async Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken)
        {
            await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
            return await context.Departments.AsNoTracking()
                .AnyAsync(department => department.Id == departmentId && department.IsActive, cancellationToken);
        }
    }
}
