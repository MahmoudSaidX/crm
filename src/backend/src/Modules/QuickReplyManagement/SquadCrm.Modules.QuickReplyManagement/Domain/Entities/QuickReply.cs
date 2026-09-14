namespace SquadCrm.Modules.QuickReplyManagement.Domain.Entities;

/// <summary>
/// Who a quick reply is available to. Deliberately two values, not three:
/// <c>Department</c> from the CRM-145 Fields Dictionary is deferred because no
/// user-to-department association exists anywhere in the system yet (see the
/// story intake's scope note), so "permitted department scope" has no subject
/// to evaluate. Adding the enum member without that model would only create a
/// scope that can never be authorized.
/// </summary>
public enum QuickReplyScope
{
    /// <summary>Available to every user; managed only with the global-template permission.</summary>
    Global,

    /// <summary>Private to one owner; visible to and editable by that owner alone.</summary>
    Personal,
}

/// <summary>
/// A reusable draft aid for common customer responses (CRM-145).
/// <para>
/// <see cref="ArabicContent"/>/<see cref="EnglishContent"/> are stored and
/// returned as inert plain text. Nothing in this module — or in the agent UI
/// this story ships — evaluates, interpolates or renders them as markup or as
/// a template, which is how the Business Rule "no arbitrary executable
/// expressions, scripts or unrestricted object/property traversal" is
/// satisfied without an allow-listed variable catalog (deferred; intake scope
/// note). A future story that introduces real placeholders must add that
/// catalog before any content is ever expanded.
/// </para>
/// <para>
/// A template never sends anything by itself (Business Rule): this module
/// exposes no send path at all.
/// </para>
/// </summary>
public sealed class QuickReply
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    /// <summary>Upper-invariant <see cref="Name"/>, backing the per-scope uniqueness indexes.</summary>
    public required string NormalizedName { get; set; }

    public string? ArabicContent { get; set; }
    public string? EnglishContent { get; set; }

    /// <summary>
    /// Fixed at creation and never changed by an update: promoting a Personal
    /// template to Global through the edit endpoint would be a
    /// privilege-escalation path around the global-template permission
    /// (see <c>QuickReplyService</c>).
    /// </summary>
    public QuickReplyScope Scope { get; set; }

    /// <summary>
    /// The owning user for <see cref="QuickReplyScope.Personal"/>; always null
    /// for <see cref="QuickReplyScope.Global"/>. Resolved server-side from the
    /// authenticated caller at creation, never read from the request body, so
    /// a caller cannot mint a template owned by someone else.
    /// </summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>
    /// Deactivation is preferred to deletion (Business Rule): an inactive
    /// template stays readable and historically identifiable but is excluded
    /// from new selection by consumers (CRM-146).
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
