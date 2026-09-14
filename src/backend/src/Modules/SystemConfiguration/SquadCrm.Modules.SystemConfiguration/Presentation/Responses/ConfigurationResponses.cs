using SquadCrm.Modules.SystemConfiguration.Domain.Policies;

namespace SquadCrm.Modules.SystemConfiguration.Presentation.Responses;

/// <summary>
/// <see cref="Value"/> is null when the key is sensitive — the raw value is
/// never returned to the frontend, only <see cref="HasValue"/> reports
/// whether an override has been set.
/// </summary>
public sealed record ConfigurationValueResponse(
    string Key,
    ConfigurationValueType ValueType,
    string DisplayNameEn,
    string DisplayNameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    string? Value,
    bool HasValue,
    string DefaultValue,
    bool IsSensitive,
    bool RequiresRestart,
    bool IsEditable,
    int? MinNumber,
    int? MaxNumber,
    string? UpdatedByHandle,
    DateTimeOffset? UpdatedAtUtc);
