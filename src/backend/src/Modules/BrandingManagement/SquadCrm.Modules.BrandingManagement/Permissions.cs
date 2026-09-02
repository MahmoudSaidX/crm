namespace SquadCrm.Modules.BrandingManagement;

public static class Permissions
{
    public const string BrandingView = "branding.view";
    public const string BrandingManage = "branding.manage";
}

internal static class PermissionPolicies
{
    public const string BrandingView = "permission:branding.view";
    public const string BrandingManage = "permission:branding.manage";
}
