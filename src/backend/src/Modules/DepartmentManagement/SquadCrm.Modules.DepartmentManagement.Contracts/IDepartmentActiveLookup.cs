namespace SquadCrm.Modules.DepartmentManagement.Contracts;

public interface IDepartmentActiveLookup
{
    Task<bool> IsActiveAsync(Guid departmentId, CancellationToken cancellationToken);
}
