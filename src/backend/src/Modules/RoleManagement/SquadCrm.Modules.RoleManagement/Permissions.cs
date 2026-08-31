namespace SquadCrm.Modules.RoleManagement;

public static class Permissions
{
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";

    public static readonly IReadOnlyList<string> Bootstrap = [RolesView, RolesManage];
}

internal static class PermissionPolicies
{
    public const string RolesView = "permission:roles.view";
    public const string RolesManage = "permission:roles.manage";
}
