namespace SquadCrm.Modules.RoleManagement;

public static class Permissions
{
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";

    public static readonly IReadOnlyList<string> Bootstrap = [RolesView, RolesManage, UsersView, UsersManage];
}

internal static class PermissionPolicies
{
    public const string RolesView = "permission:roles.view";
    public const string RolesManage = "permission:roles.manage";
    public const string UsersView = "permission:users.view";
    public const string UsersManage = "permission:users.manage";
    public const string AuditView = "permission:audit.view";
}
