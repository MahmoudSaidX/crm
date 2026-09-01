using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.DepartmentManagement;
using SquadCrm.Modules.DepartmentManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class DepartmentManagementTests
{
    public DepartmentManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_AndRecordsOneCreatedAuditEntry()
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        DepartmentService service = CreateService(context, auditRecorder, "agent@example.test");
        string code = UniqueCode();

        DepartmentMutationResult result = await service.CreateAsync(
            new CreateDepartmentRequest(code, "المبيعات", "Sales", "Handles sales pipeline"), CancellationToken.None);

        Assert.Equal(DepartmentMutationFailure.None, result.Failure);
        Assert.NotNull(result.Department);
        Assert.True(result.Department!.IsActive);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.Department.Id.ToString() && request.ActorHandle == "agent@example.test");
    }

    [Fact]
    public async Task Update_Succeeds_AndRecordsOneUpdatedAuditEntry()
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        DepartmentService service = CreateService(context, auditRecorder, "agent@example.test");
        Department department = await CreateDepartmentAsync(service);

        DepartmentMutationResult result = await service.UpdateAsync(
            department.Id,
            new UpdateDepartmentRequest(department.Code, "المبيعات المحدثة", "Updated Sales", "Updated description"),
            CancellationToken.None);

        Assert.Equal(DepartmentMutationFailure.None, result.Failure);
        Assert.Equal("Updated Sales", result.Department!.EnglishName);
        Assert.Contains(auditRecorder.Requests, request => request.Action == "updated" && request.EntityId == department.Id.ToString());
    }

    [Fact]
    public async Task Activate_And_Deactivate_EachRecordTheirOwnEvent_AndNeverDeleteTheRow()
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        DepartmentService service = CreateService(context, auditRecorder, "agent@example.test");
        Department department = await CreateDepartmentAsync(service);

        DepartmentMutationResult deactivated = await service.DeactivateAsync(department.Id, CancellationToken.None);
        Assert.Equal(DepartmentMutationFailure.None, deactivated.Failure);
        Assert.False(deactivated.Department!.IsActive);

        DepartmentMutationResult activated = await service.ActivateAsync(department.Id, CancellationToken.None);
        Assert.Equal(DepartmentMutationFailure.None, activated.Failure);
        Assert.True(activated.Department!.IsActive);

        Assert.Contains(auditRecorder.Requests, request => request.Action == "deactivated" && request.EntityId == department.Id.ToString());
        Assert.Contains(auditRecorder.Requests, request => request.Action == "activated" && request.EntityId == department.Id.ToString());

        // Deactivating never deletes: the department remains readable/listable.
        Department? stillPresent = await service.GetAsync(department.Id, CancellationToken.None);
        Assert.NotNull(stillPresent);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateCode_SameOrDifferentCaseOrWhitespace_IsRejected(string variant)
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        DepartmentService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        string original = UniqueCode();
        DepartmentMutationResult firstResult = await service.CreateAsync(
            new CreateDepartmentRequest(original, "القسم", "Department", null), CancellationToken.None);
        Assert.Equal(DepartmentMutationFailure.None, firstResult.Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        DepartmentMutationResult secondResult = await service.CreateAsync(
            new CreateDepartmentRequest(second, "قسم آخر", "Another Department", null), CancellationToken.None);

        Assert.Equal(DepartmentMutationFailure.DuplicateCode, secondResult.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using DepartmentManagementDbContext firstContext = PostgresTestDatabase.CreateDepartmentManagementContext();
        await using DepartmentManagementDbContext secondContext = PostgresTestDatabase.CreateDepartmentManagementContext();
        string code = UniqueCode();
        CreateDepartmentRequest request = new(code, "القسم", "Department", null);

        Task<DepartmentMutationResult> first = CreateService(firstContext, new RecordingAuditRecorder(), "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<DepartmentMutationResult> second = CreateService(secondContext, new RecordingAuditRecorder(), "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        DepartmentMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == DepartmentMutationFailure.None);
        Assert.Single(results, result => result.Failure == DepartmentMutationFailure.DuplicateCode);
    }

    [Fact]
    public async Task UnknownId_OnUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        DepartmentService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid unknownId = Guid.NewGuid();

        Assert.Equal(DepartmentMutationFailure.NotFound,
            (await service.UpdateAsync(unknownId, new UpdateDepartmentRequest(UniqueCode(), "قسم", "Department", null), CancellationToken.None)).Failure);
        Assert.Equal(DepartmentMutationFailure.NotFound, (await service.ActivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Equal(DepartmentMutationFailure.NotFound, (await service.DeactivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Null(await service.GetAsync(unknownId, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyDescription_IsAcceptedAsNull()
    {
        await using DepartmentManagementDbContext context = PostgresTestDatabase.CreateDepartmentManagementContext();
        DepartmentService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        DepartmentMutationResult result = await service.CreateAsync(
            new CreateDepartmentRequest(UniqueCode(), "قسم", "Department", "   "), CancellationToken.None);

        Assert.Equal(DepartmentMutationFailure.None, result.Failure);
        Assert.Null(result.Department!.Description);
    }

    private static async Task<Department> CreateDepartmentAsync(DepartmentService service)
    {
        DepartmentMutationResult result = await service.CreateAsync(
            new CreateDepartmentRequest(UniqueCode(), "قسم", "Department", null), CancellationToken.None);
        return result.Department!;
    }

    private static DepartmentService CreateService(DepartmentManagementDbContext context, IAuditRecorder auditRecorder, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle), auditRecorder);

    private static string UniqueCode() => $"DEPT_{Guid.NewGuid():N}"[..20];

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
}
