namespace SquadCrm.Modules.StaffIdentity.Persistence;

public sealed class AuthenticationEvent
{
    public long Id { get; set; }
    public Guid? StaffUserId { get; set; }
    public required string EventType { get; set; }
    public required string Outcome { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
