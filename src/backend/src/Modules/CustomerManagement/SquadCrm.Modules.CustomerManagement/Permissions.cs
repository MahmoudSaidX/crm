namespace SquadCrm.Modules.CustomerManagement;

public static class Permissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersManage = "customers.manage";
}

internal static class PermissionPolicies
{
    public const string CustomersView = "permission:customers.view";
    public const string CustomersManage = "permission:customers.manage";
}
