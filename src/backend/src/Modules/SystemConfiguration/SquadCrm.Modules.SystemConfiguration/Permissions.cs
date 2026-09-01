namespace SquadCrm.Modules.SystemConfiguration;

public static class Permissions
{
    public const string ConfigurationView = "configuration.view";
    public const string ConfigurationManage = "configuration.manage";
}

internal static class PermissionPolicies
{
    public const string ConfigurationView = "permission:configuration.view";
    public const string ConfigurationManage = "permission:configuration.manage";
}
