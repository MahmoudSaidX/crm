namespace SquadCrm.Modules.StaffIdentity.Contracts;

/// <summary>
/// <paramref name="DisplayName"/> is null when the staff user has none set —
/// consumers that need a printable name (e.g. CRM-146's AgentName variable)
/// fall back to <paramref name="NormalizedEmail"/> in that case.
/// </summary>
public sealed record StaffSubjectReference(
    Guid Id, bool IsActive, string? DisplayName = null, string? NormalizedEmail = null);

public interface IStaffSubjectReferenceReader
{
    Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task<StaffSubjectReference?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
