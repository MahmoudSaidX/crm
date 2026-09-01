using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.Modules.Audit.Persistence;

namespace SquadCrm.Modules.Audit;

public sealed class AuditQueryService(AuditDbContext dbContext)
{
    public async Task<PagedResult<AuditRecord>> ListAsync(
        PaginationRequest pagination,
        string? entityType,
        string? action,
        string? actorHandle,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        IQueryable<AuditRecord> query = dbContext.AuditRecords.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            string trimmed = entityType.Trim();
            query = query.Where(record => record.EntityType == trimmed);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            string trimmed = action.Trim();
            query = query.Where(record => record.Action == trimmed);
        }

        if (!string.IsNullOrWhiteSpace(actorHandle))
        {
            string trimmed = actorHandle.Trim();
            query = query.Where(record => record.ActorHandle == trimmed);
        }

        if (from.HasValue)
        {
            query = query.Where(record => record.OccurredAtUtc >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(record => record.OccurredAtUtc <= to.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<AuditRecord> items = await query
            .OrderByDescending(record => record.OccurredAtUtc)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<AuditRecord>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public Task<AuditRecord?> GetAsync(long id, CancellationToken cancellationToken) =>
        dbContext.AuditRecords.AsNoTracking().SingleOrDefaultAsync(record => record.Id == id, cancellationToken);
}
