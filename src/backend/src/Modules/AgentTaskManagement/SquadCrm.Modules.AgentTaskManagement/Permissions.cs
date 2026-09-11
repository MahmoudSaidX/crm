namespace SquadCrm.Modules.AgentTaskManagement;

public static class Permissions
{
    public const string TasksView = "tasks.view";
    public const string TasksCreate = "tasks.create";
    public const string TasksEdit = "tasks.edit";

    /// <summary>Completes or reopens a task (CRM-143 Business Rule: reopen is an explicit supported workflow).</summary>
    public const string TasksComplete = "tasks.complete";
}

internal static class PermissionPolicies
{
    public const string TasksView = "permission:tasks.view";
    public const string TasksCreate = "permission:tasks.create";
    public const string TasksEdit = "permission:tasks.edit";
    public const string TasksComplete = "permission:tasks.complete";
}
