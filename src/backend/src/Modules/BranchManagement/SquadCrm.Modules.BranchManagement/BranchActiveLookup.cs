using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.BranchManagement.Persistence;

namespace SquadCrm.Modules.BranchManagement;

internal sealed class BranchActiveLookup(BranchManagementDbContext dbContext) : IBranchActiveLookup
{
    public async Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken) =>
        await dbContext.Branches.AsNoTracking()
            .AnyAsync(branch => branch.Id == branchId && branch.IsActive, cancellationToken);
}
