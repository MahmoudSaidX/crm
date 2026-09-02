using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Persistence;

namespace SquadCrm.Modules.DepartmentManagement;

internal sealed class DepartmentActiveLookup(DepartmentManagementDbContext dbContext) : IDepartmentActiveLookup
{
    public async Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken) =>
        await dbContext.Departments.AsNoTracking()
            .AnyAsync(department => department.Id == departmentId && department.IsActive, cancellationToken);
}
