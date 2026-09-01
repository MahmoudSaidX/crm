namespace SquadCrm.Modules.SystemConfiguration.Persistence;

/// <summary>
/// The stored override for a registered <see cref="ConfigurationDefinition"/>
/// key. Absence of a row means the key's effective value is its catalog
/// default — defaults and overrides stay explicitly distinguishable.
/// </summary>
public sealed class ConfigurationValue
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public required string UpdatedByHandle { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
