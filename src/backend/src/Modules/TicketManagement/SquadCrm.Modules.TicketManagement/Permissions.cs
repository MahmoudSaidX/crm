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

    /// <summary>Escalate a ticket (CRM-138).</summary>
    public const string TicketsEscalate = "tickets.escalate";

    /// <summary>
    /// Write internal collaboration on a ticket — add a note, add or remove a
    /// watcher (CRM-147). One permission rather than four: collaboration is a
    /// single coherent capability, and separate note/watcher permissions would
    /// seed catalog entries no role or endpoint distinguishes today (YAGNI).
    /// Reading notes and watchers reuses <see cref="TicketsView"/>, the CRM-139
    /// precedent for a ticket's read-only sub-resources.
    /// <para>
    /// Handing a ticket off is NOT covered here: handoff is reassignment and
    /// stays behind <see cref="TicketsAssign"/> (CRM-136), so collaboration
    /// access can never become a second route to changing ownership (BR
    /// "handoff uses the canonical ticket assignment capability").
    /// </para>
    /// </summary>
    public const string TicketsCollaborate = "tickets.collaborate";
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
    public const string TicketsEscalate = "permission:tickets.escalate";
    public const string TicketsCollaborate = "permission:tickets.collaborate";
}
