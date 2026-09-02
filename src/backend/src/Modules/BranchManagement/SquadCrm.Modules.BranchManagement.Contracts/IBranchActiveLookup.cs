namespace SquadCrm.Modules.BranchManagement.Contracts;

public interface IBranchActiveLookup
{
    Task<bool> IsActiveAsync(Guid branchId, CancellationToken cancellationToken);
}
