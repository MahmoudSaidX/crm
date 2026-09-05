namespace SquadCrm.Modules.RoleManagement;

public static class Permissions
{
    public const string RolesView = "roles.view";
    public const string RolesManage = "roles.manage";
    public const string UsersView = "users.view";
    public const string UsersManage = "users.manage";
    public const string AuditView = "audit.view";
    public const string DepartmentsView = "departments.view";
    public const string DepartmentsManage = "departments.manage";
    public const string ConfigurationView = "configuration.view";
    public const string ConfigurationManage = "configuration.manage";
    public const string BranchesView = "branches.view";
    public const string BranchesManage = "branches.manage";
    public const string BrandingView = "branding.view";
    public const string BrandingManage = "branding.manage";
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
}

internal static class PermissionPolicies
{
    public const string RolesView = "permission:roles.view";
    public const string RolesManage = "permission:roles.manage";
    public const string UsersView = "permission:users.view";
    public const string UsersManage = "permission:users.manage";
    public const string AuditView = "permission:audit.view";
    public const string DepartmentsView = "permission:departments.view";
    public const string DepartmentsManage = "permission:departments.manage";
    public const string ConfigurationView = "permission:configuration.view";
    public const string ConfigurationManage = "permission:configuration.manage";
    public const string BranchesView = "permission:branches.view";
    public const string BranchesManage = "permission:branches.manage";
    public const string BrandingView = "permission:branding.view";
    public const string BrandingManage = "permission:branding.manage";
    public const string CustomersView = "permission:customers.view";
    public const string CustomersManage = "permission:customers.manage";
}
