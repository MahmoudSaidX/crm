using Microsoft.EntityFrameworkCore;
using Npgsql;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.QuickReplyManagement.Domain.Entities;
using SquadCrm.Modules.QuickReplyManagement.Infrastructure.Authorization;
using SquadCrm.Modules.QuickReplyManagement.Infrastructure.Persistence;
using SquadCrm.Modules.QuickReplyManagement.Presentation.Requests;

namespace SquadCrm.Modules.QuickReplyManagement.Application.Services;

/// <summary>Discriminates why a mutating call did not produce a <see cref="QuickReply"/>.</summary>
public enum QuickReplyMutationFailure
{
    None,

    /// <summary>The caller's authenticated handle could not be resolved to a user id — fail-closed, never a guess.</summary>
    CallerUnresolved,

    /// <summary>Another template in the same scope already uses this name.</summary>
    DuplicateName,

    /// <summary>
    /// Not found, or found but not visible to this caller. The two are
    /// deliberately the same answer for personal templates: see
    /// <see cref="FindVisibleAsync"/>.
    /// </summary>
    NotFound,

    /// <summary>A Global template was addressed without the global-template permission.</summary>
    GlobalPermissionRequired,

    /// <summary>A Personal template was addressed by someone who does not own it.</summary>
    NotOwner,

    /// <summary>Neither Arabic nor English content was supplied.</summary>
    ContentRequired,
}

public readonly record struct QuickReplyMutationResult(QuickReply? QuickReply, QuickReplyMutationFailure Failure)
{
    public static QuickReplyMutationResult Success(QuickReply quickReply) =>
        new(quickReply, QuickReplyMutationFailure.None);

    public static QuickReplyMutationResult Failed(QuickReplyMutationFailure failure) => new(null, failure);
}

/// <summary>
/// Owns the CRM-145 authorization model, which is enforced here — in the
/// application layer — and not merely hidden in the UI (Business Rule):
/// <list type="bullet">
/// <item>Global templates: every mutation requires the global-template permission.</item>
/// <item>Personal templates: every mutation requires the caller to BE the owner.
/// Holding the global permission grants nothing over someone else's drafts.</item>
/// <item>Scope is immutable after creation, so the edit endpoint cannot be used
/// to promote a personal template into a global one.</item>
/// <item>Reads return Global templates plus the caller's own Personal ones only.</item>
/// </list>
/// </summary>
internal sealed class QuickReplyService(
    QuickReplyManagementDbContext dbContext,
    ICurrentUserAccessor currentUserAccessor,
    IGlobalQuickReplyAuthorizer globalAuthorizer,
    IAuditRecorder auditRecorder)
{
    /// <summary>
    /// Postgres unique-violation SQLSTATE, used to translate a lost duplicate-name
    /// race into the same duplicate result the pre-check produces, rather than
    /// letting a 500 leak through (mirrors <c>DepartmentService</c>).
    /// </summary>
    private const string PostgresUniqueViolationSqlState = "23505";

    public async Task<QuickReplyMutationResult> CreateAsync(
        CreateQuickReplyRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCaller(out Guid callerId))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.CallerUnresolved);
        }

        if (request.Scope == QuickReplyScope.Global
            && !await globalAuthorizer.CanManageGlobalAsync(cancellationToken))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.GlobalPermissionRequired);
        }

        string? arabicContent = NormalizeContent(request.ArabicContent);
        string? englishContent = NormalizeContent(request.EnglishContent);
        if (arabicContent is null && englishContent is null)
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.ContentRequired);
        }

        // A Personal template's owner is ALWAYS the caller — never a value the
        // request could supply — so a caller cannot create a template owned by
        // someone else. A Global template has no owner at all.
        Guid? ownerUserId = request.Scope == QuickReplyScope.Personal ? callerId : null;
        string normalizedName = Normalize(request.Name);

        if (await IsDuplicateNameAsync(normalizedName, request.Scope, ownerUserId, excludedId: null, cancellationToken))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.DuplicateName);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        QuickReply quickReply = new()
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            NormalizedName = normalizedName,
            ArabicContent = arabicContent,
            EnglishContent = englishContent,
            Scope = request.Scope,
            OwnerUserId = ownerUserId,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.QuickReplies.Add(quickReply);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.DuplicateName);
        }

        await RecordAuditAsync(quickReply.Id, "created", cancellationToken);
        return QuickReplyMutationResult.Success(quickReply);
    }

    public async Task<QuickReplyMutationResult> UpdateAsync(
        Guid id,
        UpdateQuickReplyRequest request,
        CancellationToken cancellationToken)
    {
        QuickReplyMutationResult loaded = await LoadForMutationAsync(id, tracked: true, cancellationToken);
        if (loaded.Failure != QuickReplyMutationFailure.None)
        {
            return loaded;
        }

        QuickReply quickReply = loaded.QuickReply!;
        string? arabicContent = NormalizeContent(request.ArabicContent);
        string? englishContent = NormalizeContent(request.EnglishContent);
        if (arabicContent is null && englishContent is null)
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.ContentRequired);
        }

        string normalizedName = Normalize(request.Name);
        if (await IsDuplicateNameAsync(
            normalizedName, quickReply.Scope, quickReply.OwnerUserId, id, cancellationToken))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.DuplicateName);
        }

        // Scope and OwnerUserId are deliberately untouched — see the type's
        // summary: an edit must never move a template between scopes.
        quickReply.Name = request.Name.Trim();
        quickReply.NormalizedName = normalizedName;
        quickReply.ArabicContent = arabicContent;
        quickReply.EnglishContent = englishContent;
        quickReply.UpdatedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.DuplicateName);
        }

        await RecordAuditAsync(quickReply.Id, "updated", cancellationToken);
        return QuickReplyMutationResult.Success(quickReply);
    }

    /// <summary>
    /// Reads one template the caller is allowed to see (Global, or their own
    /// Personal). A template owned by someone else answers
    /// <see cref="QuickReplyMutationFailure.NotFound"/> rather than a
    /// forbidden result, so probing ids cannot confirm that another agent has a
    /// template by a given id.
    /// </summary>
    public async Task<QuickReplyMutationResult> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!TryResolveCaller(out Guid callerId))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.CallerUnresolved);
        }

        QuickReply? quickReply = await FindVisibleAsync(id, callerId, tracked: false, cancellationToken);
        return quickReply is null
            ? QuickReplyMutationResult.Failed(QuickReplyMutationFailure.NotFound)
            : QuickReplyMutationResult.Success(quickReply);
    }

    /// <summary>
    /// Global templates plus the caller's own Personal ones. Fails closed: an
    /// unresolvable caller handle yields an empty page, never the unfiltered
    /// list.
    /// </summary>
    public async Task<PagedResult<QuickReply>> ListAsync(
        QuickReplyListQuery query,
        PaginationRequest pagination,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCaller(out Guid callerId))
        {
            return new PagedResult<QuickReply>([], pagination.Page, pagination.PageSize, 0);
        }

        IQueryable<QuickReply> filtered = VisibleTo(dbContext.QuickReplies.AsNoTracking(), callerId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.Trim();
            filtered = filtered.Where(quickReply => quickReply.Name.Contains(search));
        }

        if (query.Scope is { } scope)
        {
            filtered = filtered.Where(quickReply => quickReply.Scope == scope);
        }

        if (query.ActiveOnly)
        {
            filtered = filtered.Where(quickReply => quickReply.IsActive);
        }

        // A stable tiebreaker (Id) after the name, so paginated results never
        // reorder across pages when two templates share a name across scopes.
        IOrderedQueryable<QuickReply> ordered = filtered
            .OrderBy(quickReply => quickReply.Name)
            .ThenBy(quickReply => quickReply.Id);

        int totalCount = await ordered.CountAsync(cancellationToken);
        List<QuickReply> items = await ordered
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);
        return new PagedResult<QuickReply>(items, pagination.Page, pagination.PageSize, totalCount);
    }

    public Task<QuickReplyMutationResult> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: true, "activated", cancellationToken);

    public Task<QuickReplyMutationResult> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: false, "deactivated", cancellationToken);

    private async Task<QuickReplyMutationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string action,
        CancellationToken cancellationToken)
    {
        QuickReplyMutationResult loaded = await LoadForMutationAsync(id, tracked: true, cancellationToken);
        if (loaded.Failure != QuickReplyMutationFailure.None)
        {
            return loaded;
        }

        // Never deletes the row (Business Rule: deactivation is preferred to
        // deletion after use/reference) — a deactivated template stays
        // readable and historically identifiable, and is only excluded from
        // new selection by consumers.
        QuickReply quickReply = loaded.QuickReply!;
        quickReply.IsActive = isActive;
        quickReply.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecordAuditAsync(quickReply.Id, action, cancellationToken);
        return QuickReplyMutationResult.Success(quickReply);
    }

    /// <summary>
    /// The single gate every mutation of an existing template passes through:
    /// resolve the caller, find the row the caller can see, then apply the
    /// scope rule (global permission for Global, ownership for Personal).
    /// </summary>
    private async Task<QuickReplyMutationResult> LoadForMutationAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCaller(out Guid callerId))
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.CallerUnresolved);
        }

        QuickReply? quickReply = await FindVisibleAsync(id, callerId, tracked, cancellationToken);
        if (quickReply is null)
        {
            return QuickReplyMutationResult.Failed(QuickReplyMutationFailure.NotFound);
        }

        if (quickReply.Scope == QuickReplyScope.Global)
        {
            return await globalAuthorizer.CanManageGlobalAsync(cancellationToken)
                ? QuickReplyMutationResult.Success(quickReply)
                : QuickReplyMutationResult.Failed(QuickReplyMutationFailure.GlobalPermissionRequired);
        }

        // Personal: ownership is the rule, and the global-template permission
        // deliberately does not override it.
        return quickReply.OwnerUserId == callerId
            ? QuickReplyMutationResult.Success(quickReply)
            : QuickReplyMutationResult.Failed(QuickReplyMutationFailure.NotOwner);
    }

    /// <summary>
    /// Applies the visibility rule as part of the query itself, so a personal
    /// template belonging to another user is never materialized at all.
    /// </summary>
    private Task<QuickReply?> FindVisibleAsync(
        Guid id,
        Guid callerId,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<QuickReply> source = tracked ? dbContext.QuickReplies : dbContext.QuickReplies.AsNoTracking();
        return VisibleTo(source, callerId).SingleOrDefaultAsync(quickReply => quickReply.Id == id, cancellationToken);
    }

    private static IQueryable<QuickReply> VisibleTo(IQueryable<QuickReply> source, Guid callerId) =>
        source.Where(quickReply =>
            quickReply.Scope == QuickReplyScope.Global || quickReply.OwnerUserId == callerId);

    /// <summary>
    /// Uniqueness is scoped: a Global name collides only with other Global
    /// names, and a Personal name only with the SAME owner's names — so two
    /// agents may each keep their own "Greeting".
    /// </summary>
    private Task<bool> IsDuplicateNameAsync(
        string normalizedName,
        QuickReplyScope scope,
        Guid? ownerUserId,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        dbContext.QuickReplies.AnyAsync(
            quickReply => quickReply.NormalizedName == normalizedName
                && quickReply.Scope == scope
                && quickReply.OwnerUserId == ownerUserId
                && (excludedId == null || quickReply.Id != excludedId),
            cancellationToken);

    private bool TryResolveCaller(out Guid callerId) =>
        Guid.TryParse(currentUserAccessor.Handle, out callerId);

    private Task RecordAuditAsync(Guid quickReplyId, string action, CancellationToken cancellationToken) =>
        auditRecorder.RecordAsync(
            new AuditRecordRequest(
                currentUserAccessor.Handle ?? "unknown", action, "QuickReply", quickReplyId.ToString(), Metadata: null),
            cancellationToken);

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    /// <summary>Blank-or-missing content is stored as null, so "   " never counts as a supplied language.</summary>
    private static string? NormalizeContent(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == PostgresUniqueViolationSqlState;
}
