namespace SquadCrm.Modules.BranchManagement;

public static class Permissions
{
    public const string BranchesView = "branches.view";
    public const string BranchesManage = "branches.manage";
}

internal static class PermissionPolicies
{
    public const string BranchesView = "permission:branches.view";
    public const string BranchesManage = "permission:branches.manage";
}
