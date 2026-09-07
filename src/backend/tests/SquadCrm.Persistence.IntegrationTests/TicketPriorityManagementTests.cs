using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.TicketManagement;
using SquadCrm.Modules.TicketManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class TicketPriorityManagementTests
{
    public TicketPriorityManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Succeeds_AndRecordsOneCreatedAuditEntry()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketPriorityService service = CreateService(context, auditRecorder, "agent@example.test");
        string code = UniqueCode();

        TicketPriorityMutationResult result = await service.CreateAsync(
            new CreateTicketPriorityRequest(code, "عاجل", "Urgent", 1, "Highest urgency"), CancellationToken.None);

        Assert.Equal(TicketPriorityMutationFailure.None, result.Failure);
        Assert.NotNull(result.TicketPriority);
        Assert.True(result.TicketPriority!.IsActive);
        Assert.Equal(1, result.TicketPriority.Rank);
        Assert.Single(auditRecorder.Requests, request =>
            request.Action == "created" && request.EntityId == result.TicketPriority.Id.ToString() && request.ActorHandle == "agent@example.test");
    }

    [Fact]
    public async Task Update_Succeeds_AndRecordsOneUpdatedAuditEntry()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketPriorityService service = CreateService(context, auditRecorder, "agent@example.test");
        TicketPriority priority = await CreatePriorityAsync(service);

        TicketPriorityMutationResult result = await service.UpdateAsync(
            priority.Id,
            new UpdateTicketPriorityRequest(priority.Code, "عاجل محدث", "Updated Urgent", 2, "Updated description"),
            CancellationToken.None);

        Assert.Equal(TicketPriorityMutationFailure.None, result.Failure);
        Assert.Equal("Updated Urgent", result.TicketPriority!.EnglishName);
        Assert.Equal(2, result.TicketPriority.Rank);
        Assert.Contains(auditRecorder.Requests, request => request.Action == "updated" && request.EntityId == priority.Id.ToString());
    }

    [Fact]
    public async Task Activate_And_Deactivate_EachRecordTheirOwnEvent_AndNeverDeleteTheRow()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        RecordingAuditRecorder auditRecorder = new();
        TicketPriorityService service = CreateService(context, auditRecorder, "agent@example.test");
        TicketPriority priority = await CreatePriorityAsync(service);

        TicketPriorityMutationResult deactivated = await service.DeactivateAsync(priority.Id, CancellationToken.None);
        Assert.Equal(TicketPriorityMutationFailure.None, deactivated.Failure);
        Assert.False(deactivated.TicketPriority!.IsActive);

        TicketPriorityMutationResult activated = await service.ActivateAsync(priority.Id, CancellationToken.None);
        Assert.Equal(TicketPriorityMutationFailure.None, activated.Failure);
        Assert.True(activated.TicketPriority!.IsActive);

        Assert.Contains(auditRecorder.Requests, request => request.Action == "deactivated" && request.EntityId == priority.Id.ToString());
        Assert.Contains(auditRecorder.Requests, request => request.Action == "activated" && request.EntityId == priority.Id.ToString());

        // Deactivating never deletes: the priority remains readable/listable
        // (there is no Ticket entity yet to enforce selection against).
        TicketPriority? stillPresent = await service.GetAsync(priority.Id, CancellationToken.None);
        Assert.NotNull(stillPresent);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateCode_SameOrDifferentCaseOrWhitespace_IsRejected(string variant)
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketPriorityService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        string original = UniqueCode();
        TicketPriorityMutationResult firstResult = await service.CreateAsync(
            new CreateTicketPriorityRequest(original, "أولوية", "Priority", 1, null), CancellationToken.None);
        Assert.Equal(TicketPriorityMutationFailure.None, firstResult.Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        TicketPriorityMutationResult secondResult = await service.CreateAsync(
            new CreateTicketPriorityRequest(second, "أولوية أخرى", "Another Priority", 2, null), CancellationToken.None);

        Assert.Equal(TicketPriorityMutationFailure.DuplicateCode, secondResult.Failure);
    }

    [Fact]
    public async Task ConcurrentDuplicateCreate_IsRejectedViaUniqueIndexCatchPath()
    {
        await using TicketManagementDbContext firstContext = PostgresTestDatabase.CreateTicketManagementContext();
        await using TicketManagementDbContext secondContext = PostgresTestDatabase.CreateTicketManagementContext();
        string code = UniqueCode();
        CreateTicketPriorityRequest request = new(code, "أولوية", "Priority", 1, null);

        Task<TicketPriorityMutationResult> first = CreateService(firstContext, new RecordingAuditRecorder(), "agent-one@example.test")
            .CreateAsync(request, CancellationToken.None);
        Task<TicketPriorityMutationResult> second = CreateService(secondContext, new RecordingAuditRecorder(), "agent-two@example.test")
            .CreateAsync(request, CancellationToken.None);
        TicketPriorityMutationResult[] results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Failure == TicketPriorityMutationFailure.None);
        Assert.Single(results, result => result.Failure == TicketPriorityMutationFailure.DuplicateCode);
    }

    [Fact]
    public async Task UnknownId_OnUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using TicketManagementDbContext context = PostgresTestDatabase.CreateTicketManagementContext();
        TicketPriorityService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");
        Guid unknownId = Guid.NewGuid();

        Assert.Equal(TicketPriorityMutationFailure.NotFound,
            (await service.UpdateAsync(unknownId, new UpdateTicketPriorityRequest(UniqueCode(), "أولوية", "Priority", 1, null), CancellationToken.None)).Failure);
        Assert.Equal(TicketPriorityMutationFailure.NotFound, (await service.ActivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Equal(TicketPriorityMutationFailure.NotFound, (await service.DeactivateAsync(unknownId, CancellationToken.None)).Failure);
        Assert.Null(await service.GetAsync(unknownId, CancellationToken.None));
    }

    private static async Task<TicketPriority> CreatePriorityAsync(TicketPriorityService service)
    {
        TicketPriorityMutationResult result = await service.CreateAsync(
            new CreateTicketPriorityRequest(UniqueCode(), "أولوية", "Priority", 1, null), CancellationToken.None);
        return result.TicketPriority!;
    }

    private static TicketPriorityService CreateService(TicketManagementDbContext context, IAuditRecorder auditRecorder, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle), auditRecorder);

    private static string UniqueCode() => $"PRI_{Guid.NewGuid():N}"[..20];

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
