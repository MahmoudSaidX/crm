using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.QuickReplyManagement;
using SquadCrm.Modules.QuickReplyManagement.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

/// <summary>
/// CRM-145. The emphasis is the authorization model — global-permission
/// enforcement, personal ownership, cross-user isolation and scope
/// immutability — because those are the Business Rules that must hold in the
/// backend rather than in the UI.
/// </summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class QuickReplyManagementTests
{
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public QuickReplyManagementTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task Create_Personal_Succeeds_OwnedByCaller_AndRecordsCreatedAudit()
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        RecordingAuditRecorder audit = new();
        QuickReplyService service = CreateService(context, audit, OwnerId, canManageGlobal: false);

        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(UniqueName(), "مرحباً", "Hello", QuickReplyScope.Personal),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.None, result.Failure);
        Assert.Equal(QuickReplyScope.Personal, result.QuickReply!.Scope);
        Assert.Equal(OwnerId, result.QuickReply.OwnerUserId);
        Assert.True(result.QuickReply.IsActive);
        Assert.Single(audit.Requests, request =>
            request.Action == "created" && request.EntityId == result.QuickReply.Id.ToString());
    }

    [Fact]
    public async Task Create_Global_WithoutGlobalPermission_IsRejected()
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: false);

        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(UniqueName(), null, "Hello", QuickReplyScope.Global),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.GlobalPermissionRequired, result.Failure);
    }

    [Fact]
    public async Task Create_Global_WithGlobalPermission_Succeeds_AndHasNoOwner()
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: true);

        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(UniqueName(), "مرحباً", null, QuickReplyScope.Global),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.None, result.Failure);
        Assert.Null(result.QuickReply!.OwnerUserId);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", "")]
    public async Task Create_WithNeitherLanguage_IsRejected(string? arabic, string? english)
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: false);

        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(UniqueName(), arabic, english, QuickReplyScope.Personal),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.ContentRequired, result.Failure);
    }

    [Theory]
    [InlineData("مرحباً", null)]
    [InlineData(null, "Hello")]
    public async Task Create_WithEitherSingleLanguage_Succeeds(string? arabic, string? english)
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: false);

        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(UniqueName(), arabic, english, QuickReplyScope.Personal),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.None, result.Failure);
    }

    [Theory]
    [InlineData("whitespace")]
    [InlineData("lowercase")]
    [InlineData("uppercase")]
    public async Task DuplicateName_WithinTheSameScopeAndOwner_IsRejected(string variant)
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: false);
        string original = UniqueName();
        Assert.Equal(QuickReplyMutationFailure.None, (await service.CreateAsync(
            new CreateQuickReplyRequest(original, null, "Hello", QuickReplyScope.Personal),
            CancellationToken.None)).Failure);

        string second = variant switch
        {
            "whitespace" => $"  {original}  ",
            "lowercase" => original.ToLowerInvariant(),
            "uppercase" => original.ToUpperInvariant(),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        QuickReplyMutationResult result = await service.CreateAsync(
            new CreateQuickReplyRequest(second, null, "Hello again", QuickReplyScope.Personal),
            CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.DuplicateName, result.Failure);
    }

    [Fact]
    public async Task SameName_IsAllowed_ForTwoDifferentPersonalOwners()
    {
        string name = UniqueName();
        await using QuickReplyManagementDbContext ownerContext = CreateContext();
        QuickReplyMutationResult owned = await CreateService(ownerContext, new RecordingAuditRecorder(), OwnerId, false)
            .CreateAsync(new CreateQuickReplyRequest(name, null, "Mine", QuickReplyScope.Personal), CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, owned.Failure);

        await using QuickReplyManagementDbContext otherContext = CreateContext();
        QuickReplyMutationResult other = await CreateService(otherContext, new RecordingAuditRecorder(), OtherUserId, false)
            .CreateAsync(new CreateQuickReplyRequest(name, null, "Theirs", QuickReplyScope.Personal), CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.None, other.Failure);
    }

    [Fact]
    public async Task DuplicateGlobalName_IsRejectedByThePartialUniqueIndex_NotOnlyThePreCheck()
    {
        // Two concurrent creates race past the pre-check; the Global partial
        // unique index has to be what stops the second. Postgres treats NULLs
        // as distinct, so a naive composite index over
        // (normalized_name, scope, owner_user_id) would let both through.
        string name = UniqueName();
        await using QuickReplyManagementDbContext firstContext = CreateContext();
        await using QuickReplyManagementDbContext secondContext = CreateContext();
        CreateQuickReplyRequest request = new(name, null, "Hello", QuickReplyScope.Global);

        QuickReplyMutationResult[] results = await Task.WhenAll(
            CreateService(firstContext, new RecordingAuditRecorder(), OwnerId, true)
                .CreateAsync(request, CancellationToken.None),
            CreateService(secondContext, new RecordingAuditRecorder(), OtherUserId, true)
                .CreateAsync(request, CancellationToken.None));

        Assert.Single(results, result => result.Failure == QuickReplyMutationFailure.None);
        Assert.Single(results, result => result.Failure == QuickReplyMutationFailure.DuplicateName);
    }

    [Fact]
    public async Task Update_ByNonOwner_IsRejected_EvenWithGlobalPermission()
    {
        await using QuickReplyManagementDbContext ownerContext = CreateContext();
        QuickReply personal = await CreatePersonalAsync(ownerContext, OwnerId);

        await using QuickReplyManagementDbContext intruderContext = CreateContext();
        QuickReplyService intruder = CreateService(
            intruderContext, new RecordingAuditRecorder(), OtherUserId, canManageGlobal: true);

        QuickReplyMutationResult result = await intruder.UpdateAsync(
            personal.Id, new UpdateQuickReplyRequest("Hijacked", null, "Hijacked"), CancellationToken.None);

        // NotFound, not NotOwner: another user's personal template is not even
        // visible, so its existence is never disclosed.
        Assert.Equal(QuickReplyMutationFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task Update_OfGlobalTemplate_WithoutGlobalPermission_IsRejected()
    {
        await using QuickReplyManagementDbContext adminContext = CreateContext();
        QuickReplyMutationResult created = await CreateService(adminContext, new RecordingAuditRecorder(), OwnerId, true)
            .CreateAsync(
                new CreateQuickReplyRequest(UniqueName(), null, "Shared", QuickReplyScope.Global),
                CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, created.Failure);

        await using QuickReplyManagementDbContext agentContext = CreateContext();
        QuickReplyMutationResult result = await CreateService(agentContext, new RecordingAuditRecorder(), OtherUserId, false)
            .UpdateAsync(
                created.QuickReply!.Id, new UpdateQuickReplyRequest("Changed", null, "Changed"), CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.GlobalPermissionRequired, result.Failure);
    }

    [Fact]
    public async Task Update_NeverChangesScopeOrOwner()
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReply personal = await CreatePersonalAsync(context, OwnerId);
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: true);

        QuickReplyMutationResult result = await service.UpdateAsync(
            personal.Id, new UpdateQuickReplyRequest(UniqueName(), "محدث", "Updated"), CancellationToken.None);

        Assert.Equal(QuickReplyMutationFailure.None, result.Failure);
        Assert.Equal(QuickReplyScope.Personal, result.QuickReply!.Scope);
        Assert.Equal(OwnerId, result.QuickReply.OwnerUserId);
    }

    [Fact]
    public async Task Get_AndList_ExposeGlobalTemplatesAndOnlyTheCallersOwnPersonalOnes()
    {
        string marker = Guid.NewGuid().ToString("N")[..10];
        await using QuickReplyManagementDbContext seedContext = CreateContext();
        QuickReplyService admin = CreateService(seedContext, new RecordingAuditRecorder(), OwnerId, canManageGlobal: true);
        QuickReplyMutationResult global = await admin.CreateAsync(
            new CreateQuickReplyRequest($"{marker} global", null, "Shared", QuickReplyScope.Global), CancellationToken.None);
        QuickReplyMutationResult mine = await admin.CreateAsync(
            new CreateQuickReplyRequest($"{marker} mine", null, "Mine", QuickReplyScope.Personal), CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, global.Failure);
        Assert.Equal(QuickReplyMutationFailure.None, mine.Failure);

        await using QuickReplyManagementDbContext otherContext = CreateContext();
        QuickReplyService other = CreateService(otherContext, new RecordingAuditRecorder(), OtherUserId, canManageGlobal: false);
        QuickReplyMutationResult theirs = await other.CreateAsync(
            new CreateQuickReplyRequest($"{marker} theirs", null, "Theirs", QuickReplyScope.Personal), CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, theirs.Failure);

        PagedResult<QuickReply> page = await other.ListAsync(
            new QuickReplyListQuery(Search: marker), new PaginationRequest(1, 50), CancellationToken.None);

        List<Guid> visible = page.Items.Select(item => item.Id).ToList();
        Assert.Contains(global.QuickReply!.Id, visible);
        Assert.Contains(theirs.QuickReply!.Id, visible);
        Assert.DoesNotContain(mine.QuickReply!.Id, visible);

        // And the same rule on the single-item read path.
        Assert.Equal(
            QuickReplyMutationFailure.NotFound,
            (await other.GetAsync(mine.QuickReply.Id, CancellationToken.None)).Failure);
        Assert.Equal(
            QuickReplyMutationFailure.None,
            (await other.GetAsync(global.QuickReply.Id, CancellationToken.None)).Failure);
    }

    [Fact]
    public async Task List_WithUnresolvableCaller_ReturnsNothing_RatherThanEverything()
    {
        await using QuickReplyManagementDbContext seedContext = CreateContext();
        await CreatePersonalAsync(seedContext, OwnerId);

        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = new(
            context, new StubCurrentUserAccessor("not-a-guid"), new StubGlobalAuthorizer(false), new RecordingAuditRecorder());

        PagedResult<QuickReply> page = await service.ListAsync(
            new QuickReplyListQuery(), new PaginationRequest(1, 50), CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    [Fact]
    public async Task ActiveOnly_ExcludesDeactivated_WhileDeactivationNeverDeletesTheRow()
    {
        string marker = Guid.NewGuid().ToString("N")[..10];
        await using QuickReplyManagementDbContext context = CreateContext();
        RecordingAuditRecorder audit = new();
        QuickReplyService service = CreateService(context, audit, OwnerId, canManageGlobal: false);
        QuickReplyMutationResult created = await service.CreateAsync(
            new CreateQuickReplyRequest($"{marker} retired", null, "Retired", QuickReplyScope.Personal),
            CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, created.Failure);
        Guid id = created.QuickReply!.Id;

        Assert.Equal(QuickReplyMutationFailure.None, (await service.DeactivateAsync(id, CancellationToken.None)).Failure);

        PagedResult<QuickReply> activeOnly = await service.ListAsync(
            new QuickReplyListQuery(Search: marker, ActiveOnly: true), new PaginationRequest(1, 50), CancellationToken.None);
        Assert.DoesNotContain(id, activeOnly.Items.Select(item => item.Id));

        // Still readable and historically identifiable — deactivation is not deletion.
        QuickReplyMutationResult stillThere = await service.GetAsync(id, CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, stillThere.Failure);
        Assert.False(stillThere.QuickReply!.IsActive);

        Assert.Equal(QuickReplyMutationFailure.None, (await service.ActivateAsync(id, CancellationToken.None)).Failure);
        Assert.Contains(audit.Requests, request => request.Action == "deactivated" && request.EntityId == id.ToString());
        Assert.Contains(audit.Requests, request => request.Action == "activated" && request.EntityId == id.ToString());
    }

    [Fact]
    public async Task UnknownId_OnGetUpdateActivateDeactivate_ReturnsNotFound_NeverThrows()
    {
        await using QuickReplyManagementDbContext context = CreateContext();
        QuickReplyService service = CreateService(context, new RecordingAuditRecorder(), OwnerId, canManageGlobal: true);
        Guid unknown = Guid.NewGuid();

        Assert.Equal(QuickReplyMutationFailure.NotFound, (await service.GetAsync(unknown, CancellationToken.None)).Failure);
        Assert.Equal(QuickReplyMutationFailure.NotFound, (await service.UpdateAsync(
            unknown, new UpdateQuickReplyRequest("Name", null, "Body"), CancellationToken.None)).Failure);
        Assert.Equal(QuickReplyMutationFailure.NotFound, (await service.ActivateAsync(unknown, CancellationToken.None)).Failure);
        Assert.Equal(QuickReplyMutationFailure.NotFound, (await service.DeactivateAsync(unknown, CancellationToken.None)).Failure);
    }

    private static async Task<QuickReply> CreatePersonalAsync(QuickReplyManagementDbContext context, Guid ownerId)
    {
        QuickReplyMutationResult result = await CreateService(context, new RecordingAuditRecorder(), ownerId, false)
            .CreateAsync(
                new CreateQuickReplyRequest(UniqueName(), null, "Hello", QuickReplyScope.Personal),
                CancellationToken.None);
        Assert.Equal(QuickReplyMutationFailure.None, result.Failure);
        return result.QuickReply!;
    }

    private static QuickReplyManagementDbContext CreateContext() =>
        PostgresTestDatabase.CreateQuickReplyManagementContext();

    private static QuickReplyService CreateService(
        QuickReplyManagementDbContext context,
        IAuditRecorder auditRecorder,
        Guid callerId,
        bool canManageGlobal) =>
        new(context,
            new StubCurrentUserAccessor(callerId.ToString()),
            new StubGlobalAuthorizer(canManageGlobal),
            auditRecorder);

    private static string UniqueName() => $"QR {Guid.NewGuid():N}"[..20];

    private sealed class StubCurrentUserAccessor(string? handle) : ICurrentUserAccessor
    {
        public bool IsAuthenticated => true;
        public string? Handle => handle;
    }

    private sealed class StubGlobalAuthorizer(bool canManageGlobal) : IGlobalQuickReplyAuthorizer
    {
        public Task<bool> CanManageGlobalAsync(CancellationToken cancellationToken) =>
            Task.FromResult(canManageGlobal);
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

/// <summary>Migration-integrity assertions for <c>QuickReplyManagementDbContext</c> (CRM-145).</summary>
[Collection(PostgresTestDatabase.CollectionName)]
public sealed class QuickReplyManagementMigrationTests
{
    [Fact]
    public async Task Migrations_ApplyToACleanDatabase()
    {
        await using QuickReplyManagementDbContext context = PostgresTestDatabase.CreateQuickReplyManagementContext();

        await context.Database.MigrateAsync();

        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(applied);
    }

    [Fact]
    public async Task NoMigrationsRemainPending()
    {
        await using QuickReplyManagementDbContext context = PostgresTestDatabase.CreateQuickReplyManagementContext();

        IEnumerable<string> pending = await context.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }
}
