using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Persistence;

namespace SquadCrm.Modules.StaffIdentity.Application.Services;

public sealed class StaffSubjectReferenceReader(StaffIdentityDbContext dbContext)
    : IStaffSubjectReferenceReader
{
    public Task<StaffSubjectReference?> FindByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        dbContext.StaffUsers
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedEmail)
            .Select(user => new StaffSubjectReference(user.Id, user.IsActive, user.DisplayName, user.NormalizedEmail))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<StaffSubjectReference?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.StaffUsers
            .AsNoTracking()
            .Where(user => user.Id == id)
            .Select(user => new StaffSubjectReference(user.Id, user.IsActive, user.DisplayName, user.NormalizedEmail))
            .SingleOrDefaultAsync(cancellationToken);
}
