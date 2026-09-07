namespace SquadCrm.Modules.TicketManagement;

public static class Permissions
{
    public const string TicketCategoriesView = "ticketcategories.view";
    public const string TicketCategoriesManage = "ticketcategories.manage";
}

internal static class PermissionPolicies
{
    public const string TicketCategoriesView = "permission:ticketcategories.view";
    public const string TicketCategoriesManage = "permission:ticketcategories.manage";
}
