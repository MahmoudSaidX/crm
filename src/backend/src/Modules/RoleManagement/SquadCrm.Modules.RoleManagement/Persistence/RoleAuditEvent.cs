namespace SquadCrm.Modules.RoleManagement.Persistence;

/// <summary>
/// Plain audit row inserted in the same <c>SaveChangesAsync</c> call as the
/// <see cref="Role"/> mutation it records — mirrors <c>AuthenticationEvent</c>
/// (see <c>StaffIdentity/AuthenticationService.cs</c>). No cross-module
/// outbox/domain-event machinery: nothing outside this story currently needs
/// to observe a role change.
/// <para>
/// Deliberately no <c>Outcome</c> field (unlike <c>AuthenticationEvent</c>):
/// every role-admin action here is a transactional CRUD write with no
/// partial-failure outcome to record — validation failures never reach this
/// table at all.
/// </para>
/// </summary>
public sealed class RoleAuditEvent
{
    public long Id { get; set; }
    public Guid RoleId { get; set; }
    public required string EventType { get; set; }
    public string? ChangedByHandle { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
