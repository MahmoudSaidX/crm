namespace SquadCrm.Modules.TicketManagement;

public static class Permissions
{
    public const string TicketCategoriesView = "ticketcategories.view";
    public const string TicketCategoriesManage = "ticketcategories.manage";
    public const string TicketPrioritiesView = "ticketpriorities.view";
    public const string TicketPrioritiesManage = "ticketpriorities.manage";
    public const string TicketsCreate = "tickets.create";

    /// <summary>
    /// Unused by this story's single POST endpoint — added now because
    /// RoleManagement's permission catalog convention seeds view+manage/create
    /// pairs together, and CRM-134/135 (browse/view) will need it; reusing the
    /// same migration now avoids a near-duplicate permission-seed migration
    /// next story.
    /// </summary>
    public const string TicketsView = "tickets.view";

    /// <summary>Assign or reassign a ticket's owning agent (CRM-136).</summary>
    public const string TicketsAssign = "tickets.assign";

    /// <summary>Move a ticket through its lifecycle statuses (CRM-137).</summary>
    public const string TicketsChangeStatus = "tickets.changestatus";
}

internal static class PermissionPolicies
{
    public const string TicketCategoriesView = "permission:ticketcategories.view";
    public const string TicketCategoriesManage = "permission:ticketcategories.manage";
    public const string TicketPrioritiesView = "permission:ticketpriorities.view";
    public const string TicketPrioritiesManage = "permission:ticketpriorities.manage";
    public const string TicketsCreate = "permission:tickets.create";
    public const string TicketsView = "permission:tickets.view";
    public const string TicketsAssign = "permission:tickets.assign";
    public const string TicketsChangeStatus = "permission:tickets.changestatus";
}
