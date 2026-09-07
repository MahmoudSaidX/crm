namespace SquadCrm.Modules.TicketManagement;

public static class Permissions
{
    public const string TicketCategoriesView = "ticketcategories.view";
    public const string TicketCategoriesManage = "ticketcategories.manage";
    public const string TicketPrioritiesView = "ticketpriorities.view";
    public const string TicketPrioritiesManage = "ticketpriorities.manage";
}

internal static class PermissionPolicies
{
    public const string TicketCategoriesView = "permission:ticketcategories.view";
    public const string TicketCategoriesManage = "permission:ticketcategories.manage";
    public const string TicketPrioritiesView = "permission:ticketpriorities.view";
    public const string TicketPrioritiesManage = "permission:ticketpriorities.manage";
}
