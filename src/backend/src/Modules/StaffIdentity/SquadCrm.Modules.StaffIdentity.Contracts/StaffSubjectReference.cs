namespace SquadCrm.Modules.StaffIdentity.Contracts;

public sealed record StaffSubjectReference(Guid Id, bool IsActive);

public interface IStaffSubjectReferenceReader
{
    Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);
}
