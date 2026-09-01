using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.SystemConfiguration;
using SquadCrm.Modules.SystemConfiguration.Persistence;

namespace SquadCrm.Persistence.IntegrationTests;

[Collection(PostgresTestDatabase.CollectionName)]
public sealed class SystemConfigurationTests
{
    private const string NonSensitiveKey = "general.company_display_name";
    private const string RangedNumberKey = "tickets.default_page_size";
    private const string BooleanKey = "notifications.email_enabled";
    private const string SensitiveKey = "integrations.smtp_password";

    public SystemConfigurationTests(PostgresTestDatabase database) => _ = database;

    [Fact]
    public async Task List_WithNoOverrides_ReturnsCatalogDefaults()
    {
        await using SystemConfigurationDbContext context = PostgresTestDatabase.CreateSystemConfigurationContext();
        ConfigurationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        IReadOnlyList<ConfigurationValueResponse> values = await service.ListAsync(CancellationToken.None);

        ConfigurationValueResponse companyName = Assert.Single(values, value => value.Key == NonSensitiveKey);
        Assert.False(companyName.HasValue);
        Assert.Equal(companyName.DefaultValue, companyName.Value);
    }

    [Fact]
    public async Task Update_NonSensitiveKey_Succeeds_AndRecordsAuditEntryWithValueMetadata()
    {
        await using SystemConfigurationDbContext context = PostgresTestDatabase.CreateSystemConfigurationContext();
        RecordingAuditRecorder auditRecorder = new();
        ConfigurationService service = CreateService(context, auditRecorder, "agent@example.test");

        ConfigurationUpdateResult result = await service.UpdateAsync(
            NonSensitiveKey, new UpdateConfigurationValueRequest("Contoso CRM"), CancellationToken.None);

        Assert.Equal(ConfigurationUpdateFailure.None, result.Failure);
        Assert.Equal("Contoso CRM", result.Value!.Value);
        Assert.True(result.Value.HasValue);
        AuditRecordRequest audit = Assert.Single(auditRecorder.Requests);
        Assert.Equal("updated", audit.Action);
        Assert.Equal(NonSensitiveKey, audit.EntityId);
        Assert.Equal("Contoso CRM", audit.Metadata?["value"]);
    }

    [Fact]
    public async Task Update_SensitiveKey_Succeeds_ButNeverReturnsOrAuditsTheRawValue()
    {
        await using SystemConfigurationDbContext context = PostgresTestDatabase.CreateSystemConfigurationContext();
        RecordingAuditRecorder auditRecorder = new();
        ConfigurationService service = CreateService(context, auditRecorder, "agent@example.test");

        ConfigurationUpdateResult result = await service.UpdateAsync(
            SensitiveKey, new UpdateConfigurationValueRequest("super-secret"), CancellationToken.None);

        Assert.Equal(ConfigurationUpdateFailure.None, result.Failure);
        Assert.Null(result.Value!.Value);
        Assert.True(result.Value.HasValue);
        AuditRecordRequest audit = Assert.Single(auditRecorder.Requests);
        Assert.Null(audit.Metadata);

        IReadOnlyList<ConfigurationValueResponse> listed = await service.ListAsync(CancellationToken.None);
        Assert.Null(Assert.Single(listed, value => value.Key == SensitiveKey).Value);
    }

    [Theory]
    [InlineData(RangedNumberKey, "9")]
    [InlineData(RangedNumberKey, "101")]
    [InlineData(RangedNumberKey, "not-a-number")]
    [InlineData(BooleanKey, "maybe")]
    public async Task Update_InvalidValueForType_IsRejected_AndLeavesNoOverrideRow(string key, string invalidValue)
    {
        await using SystemConfigurationDbContext context = PostgresTestDatabase.CreateSystemConfigurationContext();
        ConfigurationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        ConfigurationUpdateResult result = await service.UpdateAsync(
            key, new UpdateConfigurationValueRequest(invalidValue), CancellationToken.None);

        Assert.Equal(ConfigurationUpdateFailure.InvalidValue, result.Failure);
        Assert.False(Assert.Single(await service.ListAsync(CancellationToken.None), value => value.Key == key).HasValue);
    }

    [Fact]
    public async Task Update_UnknownKey_ReturnsNotFound()
    {
        await using SystemConfigurationDbContext context = PostgresTestDatabase.CreateSystemConfigurationContext();
        ConfigurationService service = CreateService(context, new RecordingAuditRecorder(), "agent@example.test");

        ConfigurationUpdateResult result = await service.UpdateAsync(
            "not.a.registered.key", new UpdateConfigurationValueRequest("value"), CancellationToken.None);

        Assert.Equal(ConfigurationUpdateFailure.NotFound, result.Failure);
    }

    private static ConfigurationService CreateService(SystemConfigurationDbContext context, IAuditRecorder auditRecorder, string? handle) =>
        new(context, new StubCurrentUserAccessor(handle), auditRecorder);

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
