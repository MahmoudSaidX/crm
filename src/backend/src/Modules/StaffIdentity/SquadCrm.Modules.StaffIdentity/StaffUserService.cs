using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Modules.StaffIdentity;

internal enum StaffUserMutationFailure
{
    None,
    DuplicateEmail,
    NotFound,
}

internal readonly record struct StaffUserMutationResult(StaffUser? User, StaffUserMutationFailure Failure)
{
    public static StaffUserMutationResult Success(StaffUser user) => new(user, StaffUserMutationFailure.None);
    public static StaffUserMutationResult Failed(StaffUserMutationFailure failure) => new(null, failure);
}

internal sealed class StaffUserService(
    StaffIdentityDbContext dbContext,
    IPasswordHasher<StaffUser> passwordHasher,
    ICurrentUserAccessor currentUserAccessor)
{
    public async Task<StaffUserMutationResult> CreateAsync(
        CreateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = AuthenticationService.NormalizeEmail(request.Email);
        if (await dbContext.StaffUsers.AnyAsync(user => user.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return StaffUserMutationResult.Failed(StaffUserMutationFailure.DuplicateEmail);
        }

        StaffUser user = new()
        {
            Id = Guid.NewGuid(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = string.Empty,
            DisplayName = Normalize(request.DisplayName),
            Department = Normalize(request.Department),
            Branch = Normalize(request.Branch),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        dbContext.StaffUsers.Add(user);
        await RecordEventAsync(user.Id, "user_created", cancellationToken);
        return StaffUserMutationResult.Success(user);
    }

    public async Task<StaffUserMutationResult> UpdateAsync(
        Guid id,
        UpdateStaffUserRequest request,
        CancellationToken cancellationToken)
    {
        StaffUser? user = await dbContext.StaffUsers.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return StaffUserMutationResult.Failed(StaffUserMutationFailure.NotFound);
        }

        user.DisplayName = Normalize(request.DisplayName);
        user.Department = Normalize(request.Department);
        user.Branch = Normalize(request.Branch);
        await RecordEventAsync(user.Id, "user_updated", cancellationToken);
        return StaffUserMutationResult.Success(user);
    }

    public Task<StaffUser?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.StaffUsers.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public async Task<PagedResult<StaffUser>> ListAsync(
        PaginationRequest pagination,
        string? search,
        CancellationToken cancellationToken)
    {
        IQueryable<StaffUser> query = dbContext.StaffUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(user =>
                user.NormalizedEmail.Contains(normalizedSearch)
                || (user.DisplayName != null && user.DisplayName.ToUpper().Contains(normalizedSearch)));
        }

        int totalCount = await query.CountAsync(cancellationToken);
        List<StaffUser> items = await query
            .OrderBy(user => user.NormalizedEmail)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<StaffUser>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public Task<StaffUserMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, "user_activated", cancellationToken);

    public Task<StaffUserMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, "user_deactivated", cancellationToken);

    private async Task<StaffUserMutationResult> SetActiveAsync(
        Guid id,
        bool active,
        string eventType,
        CancellationToken cancellationToken)
    {
        StaffUser? user = await dbContext.StaffUsers.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return StaffUserMutationResult.Failed(StaffUserMutationFailure.NotFound);
        }

        user.IsActive = active;
        await RecordEventAsync(user.Id, eventType, cancellationToken);
        return StaffUserMutationResult.Success(user);
    }

    private async Task RecordEventAsync(Guid staffUserId, string eventType, CancellationToken cancellationToken)
    {
        dbContext.AuthenticationEvents.Add(new AuthenticationEvent
        {
            StaffUserId = staffUserId,
            EventType = eventType,
            Outcome = "succeeded",
            ChangedByHandle = currentUserAccessor.Handle,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
