namespace SquadCrm.Modules.DepartmentManagement;

public static class Permissions
{
    public const string DepartmentsView = "departments.view";
    public const string DepartmentsManage = "departments.manage";
}

internal static class PermissionPolicies
{
    public const string DepartmentsView = "permission:departments.view";
    public const string DepartmentsManage = "permission:departments.manage";
}
