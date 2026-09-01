using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.SystemConfiguration.Persistence;

namespace SquadCrm.Modules.SystemConfiguration;

public enum ConfigurationUpdateFailure
{
    None,
    NotFound,
    InvalidValue,
}

public readonly record struct ConfigurationUpdateResult(ConfigurationValueResponse? Value, ConfigurationUpdateFailure Failure)
{
    public static ConfigurationUpdateResult Success(ConfigurationValueResponse value) => new(value, ConfigurationUpdateFailure.None);
    public static ConfigurationUpdateResult Failed(ConfigurationUpdateFailure failure) => new(null, failure);
}

/// <summary>
/// The canonical read/write service for the registered configuration
/// catalog. Runtime consumers that need a typed effective value are
/// expected to depend on this service; none exist yet for CRM-115's scope.
/// </summary>
internal sealed class ConfigurationService(
    SystemConfigurationDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IAuditRecorder auditRecorder)
{
    public async Task<IReadOnlyList<ConfigurationValueResponse>> ListAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, ConfigurationValue> overrides = await dbContext.ConfigurationValues
            .AsNoTracking()
            .ToDictionaryAsync(value => value.Key, cancellationToken);

        return ConfigurationCatalog.Definitions
            .Select(definition => ToResponse(definition, overrides.GetValueOrDefault(definition.Key)))
            .ToList();
    }

    public async Task<ConfigurationUpdateResult> UpdateAsync(
        string key, UpdateConfigurationValueRequest request, CancellationToken cancellationToken)
    {
        ConfigurationDefinition? definition = ConfigurationCatalog.Find(key);
        if (definition is null)
        {
            return ConfigurationUpdateResult.Failed(ConfigurationUpdateFailure.NotFound);
        }

        if (!IsValid(definition, request.Value))
        {
            return ConfigurationUpdateResult.Failed(ConfigurationUpdateFailure.InvalidValue);
        }

        ConfigurationValue? existing = await dbContext.ConfigurationValues
            .SingleOrDefaultAsync(value => value.Key == key, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        string actor = currentUserAccessor.Handle ?? "unknown";

        if (existing is null)
        {
            existing = new ConfigurationValue { Key = key, Value = request.Value, UpdatedByHandle = actor, UpdatedAtUtc = now };
            dbContext.ConfigurationValues.Add(existing);
        }
        else
        {
            existing.Value = request.Value;
            existing.UpdatedByHandle = actor;
            existing.UpdatedAtUtc = now;
        }

        // Invalid values are rejected before any row is touched (above), so a
        // failed save never leaves a partially applied override in place.
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditRecorder.RecordAsync(
            new AuditRecordRequest(
                actor,
                "updated",
                "ConfigurationValue",
                key,
                Metadata: definition.IsSensitive
                    ? null
                    : new Dictionary<string, string> { ["value"] = request.Value }),
            cancellationToken);

        return ConfigurationUpdateResult.Success(ToResponse(definition, existing));
    }

    private static bool IsValid(ConfigurationDefinition definition, string value) => definition.ValueType switch
    {
        ConfigurationValueType.Boolean => bool.TryParse(value, out _),
        ConfigurationValueType.Number => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
            && (definition.MinNumber is null || number >= definition.MinNumber)
            && (definition.MaxNumber is null || number <= definition.MaxNumber),
        ConfigurationValueType.String => true,
        _ => false,
    };

    private static ConfigurationValueResponse ToResponse(ConfigurationDefinition definition, ConfigurationValue? overrideValue) =>
        new(
            definition.Key,
            definition.ValueType,
            definition.DisplayNameEn,
            definition.DisplayNameAr,
            definition.DescriptionEn,
            definition.DescriptionAr,
            Value: definition.IsSensitive ? null : overrideValue?.Value ?? definition.DefaultValue,
            HasValue: overrideValue is not null,
            definition.DefaultValue,
            definition.IsSensitive,
            definition.RequiresRestart,
            IsEditable: true,
            definition.MinNumber,
            definition.MaxNumber,
            overrideValue?.UpdatedByHandle,
            overrideValue?.UpdatedAtUtc);
}
