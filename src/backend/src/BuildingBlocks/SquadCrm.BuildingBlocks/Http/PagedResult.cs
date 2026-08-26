namespace SquadCrm.BuildingBlocks.Http;

/// <summary>
/// Standard paged-result envelope for any module endpoint returning a page of
/// items. <see cref="Page"/> is 1-based; <see cref="TotalCount"/> is the total
/// number of items across all pages, not just this page's <see cref="Items"/>.
/// </summary>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    int TotalCount);
