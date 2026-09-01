using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.BranchManagement;
using SquadCrm.Modules.BranchManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class BranchManagementTests
{
    public BranchManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_AndRecordsOneCreatedAuditEntry()
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        BranchService service = CreateService(context, auditRecorder, "agent@example.test");
        string code = UniqueCode();

        BranchMutationResult result = await service.CreateAsync(
            new CreateBranchRequest(code, "المبيعات", "Sales", "Handles sales pipeline"), CancellationToken.None);

        Assert.Equal(BranchMutationFailure.None, result.Failure);
        Assert.NotNull(result.Branch);
        Assert.True(result.Branch!.IsActive);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.Branch.Id.ToString() && request.ActorHandle == "agent@example.test");
    }

    [Fact]
    public async Task Update_Succeeds_AndRecordsOneUpdatedAuditEntry()
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        BranchService service = CreateService(context, auditRecorder, "agent@example.test");
        Branch branch = await CreateBranchAsync(service);

        BranchMutationResult result = await service.UpdateAsync(
            branch.Id,
            new UpdateBranchRequest(branch.Code, "المبيعات المحدثة", "Updated Sales", "Updated description"),
            CancellationToken.None);

        Assert.Equal(BranchMutationFailure.None, result.Failure);
        Assert.Equal("Updated Sales", result.Branch!.EnglishName);
        Assert.Contains(auditRecorder.Requests, request => request.Action == "updated" && request.EntityId == branch.Id.ToString());
    }

    [Fact]
    public async Task Activate_And_Deactivate_EachRecordTheirOwnEvent_AndNeverDeleteTheRow()
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        BranchService service = CreateService(context, auditRecorder, "agent@example.test");
        Branch branch = await CreateBranchAsync(service);

        BranchMutationResult deactivated = await service.DeactivateAsync(branch.Id, CancellationToken.None);
        Assert.Equal(BranchMutationFailure.None, deactivated.Failure);
        Assert.False(deactivated.Branch!.IsActive);

        BranchMutationResult activated = await service.ActivateAsync(branch.Id, CancellationToken.None);
        Assert.Equal(BranchMutationFailure.None, activated.Failure);
        Assert.True(activated.Branch!.IsActive);

        Assert.Contains(auditRecorder.Requests, request => request.Action == "deactivated" && request.EntityId == branch.Id.ToString());
        Assert.Contains(auditRecorder.Requests, request => request.Action == "activated" && request.EntityId == branch.Id.ToString());

        // Deactivating never deletes: the branch remains readable/listable.
        Branch? stillPresent = await service.GetAsync(branch.Id, CancellationToken.None);
        Assert.NotNull(stillPresent);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateCode_SameOrDifferentCaseOrWhitespace_IsRejected(string variant)
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        BranchService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        string original = UniqueCode();
        BranchMutationResult firstResult = await service.CreateAsync(
            new CreateBranchRequest(original, "القسم", "Branch", null), CancellationToken.None);
        Assert.Equal(BranchMutationFailure.None, firstResult.Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        BranchMutationResult secondResult = await service.CreateAsync(
            new CreateBranchRequest(second, "قسم آخر", "Another Branch", null), CancellationToken.None);

        Assert.Equal(BranchMutationFailure.DuplicateCode, secondResult.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using BranchManagementDbContext firstContext = PostgresTestDatabase.CreateBranchManagementContext();
        await using BranchManagementDbContext secondContext = PostgresTestDatabase.CreateBranchManagementContext();
        string code = UniqueCode();
        CreateBranchRequest request = new(code, "القسم", "Branch", null);

        Task<BranchMutationResult> first = CreateService(firstContext, new RecordingAuditRecorder(), "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<BranchMutationResult> second = CreateService(secondContext, new RecordingAuditRecorder(), "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        BranchMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == BranchMutationFailure.None);
        Assert.Single(results, result => result.Failure == BranchMutationFailure.DuplicateCode);
    }

    [Fact]
    public async Task UnknownId_OnUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        BranchService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid unknownId = Guid.NewGuid();

        Assert.Equal(BranchMutationFailure.NotFound,
            (await service.UpdateAsync(unknownId, new UpdateBranchRequest(UniqueCode(), "قسم", "Branch", null), CancellationToken.None)).Failure);
        Assert.Equal(BranchMutationFailure.NotFound, (await service.ActivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Equal(BranchMutationFailure.NotFound, (await service.DeactivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Null(await service.GetAsync(unknownId, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyDescription_IsAcceptedAsNull()
    {
        await using BranchManagementDbContext context = PostgresTestDatabase.CreateBranchManagementContext();
        BranchService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        BranchMutationResult result = await service.CreateAsync(
            new CreateBranchRequest(UniqueCode(), "قسم", "Branch", "   "), CancellationToken.None);

        Assert.Equal(BranchMutationFailure.None, result.Failure);
        Assert.Null(result.Branch!.Description);
    }

    private static async Task<Branch> CreateBranchAsync(BranchService service)
    {
        BranchMutationResult result = await service.CreateAsync(
            new CreateBranchRequest(UniqueCode(), "قسم", "Branch", null), CancellationToken.None);
        return result.Branch!;
    }

    private static BranchService CreateService(BranchManagementDbContext context, IAuditRecorder auditRecorder, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle), auditRecorder);

    private static string UniqueCode() => $"BRANCH_{Guid.NewGuid():N}"[..20];

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
