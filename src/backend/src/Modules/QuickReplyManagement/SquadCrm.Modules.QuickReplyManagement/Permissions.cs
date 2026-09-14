namespace SquadCrm.Modules.QuickReplyManagement;

public static class Permissions
{
    public const string QuickRepliesView = "quickreplies.view";

    /// <summary>Manage one's OWN personal templates. Grants nothing over another user's templates.</summary>
    public const string QuickRepliesManage = "quickreplies.manage";

    /// <summary>
    /// Global-template administration (CRM-145 Business Rule). Required to
    /// create or change a <c>Global</c> template, and deliberately NOT a
    /// licence to edit another user's personal templates.
    /// </summary>
    public const string QuickRepliesManageGlobal = "quickreplies.manageglobal";
}

internal static class PermissionPolicies
{
    public const string QuickRepliesView = "permission:quickreplies.view";
    public const string QuickRepliesManage = "permission:quickreplies.manage";
    public const string QuickRepliesManageGlobal = "permission:quickreplies.manageglobal";
}
