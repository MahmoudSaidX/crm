namespace SquadCrm.Modules.StaffIdentity.Domain.Entities;

public sealed class AuthenticationEvent
{
    public long Id { get; set; }
    public Guid? StaffUserId { get; set; }
    public required string EventType { get; set; }
    public required string Outcome { get; set; }
    public string? ChangedByHandle { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}
