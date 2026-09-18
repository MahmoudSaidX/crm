using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SquadCrm.Modules.QuickReplyManagement.Domain.Entities;

namespace SquadCrm.Modules.QuickReplyManagement.Presentation.Requests;

/// <summary>
/// <paramref name="Scope"/> is fixed at creation — there is no scope field on
/// <see cref="UpdateQuickReplyRequest"/>, because a Personal-to-Global change
/// through the edit endpoint would bypass the global-template permission.
/// <para>
/// There is deliberately no <c>OwnerUserId</c>: a Personal template's owner is
/// always the authenticated caller, resolved server-side.
/// </para>
/// </summary>
public sealed record CreateQuickReplyRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(4000)] string? ArabicContent,
    [property: MaxLength(4000)] string? EnglishContent,

    /// <summary>
    /// Serialized by NAME ("Global"/"Personal"), matching how
    /// <see cref="QuickReplyResponse.Scope"/> is written. Without the
    /// converter the body binder would accept only the underlying integer,
    /// so a client echoing back a scope it had just read would fail to bind.
    /// </summary>
    [property: Required, JsonConverter(typeof(JsonStringEnumConverter))] QuickReplyScope Scope);

/// <summary>
/// Scope and owner are immutable after creation and so are absent here; both
/// activation state and deletion are separate concerns (activate/deactivate
/// endpoints; deletion is not offered — deactivation is preferred, per the
/// Business Rules).
/// </summary>
public sealed record UpdateQuickReplyRequest(
    [property: Required, MaxLength(200)] string Name,
    [property: MaxLength(4000)] string? ArabicContent,
    [property: MaxLength(4000)] string? EnglishContent);

/// <summary>
/// Bound via <c>[AsParameters]</c> alongside
/// <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/>.
/// <para>
/// There is no owner filter: which templates a caller may see is decided
/// server-side (Global plus the caller's own), never by a client-supplied id.
/// </para>
/// </summary>
public sealed record QuickReplyListQuery(
    [property: MaxLength(200)] string? Search = null,
    QuickReplyScope? Scope = null,

    /// <summary>
    /// When true, only active templates are returned — what CRM-146 will ask
    /// for, since an inactive template "cannot be newly selected" (AC) while
    /// remaining readable here for management and history.
    /// </summary>
    bool ActiveOnly = false);

/// <summary>
/// <paramref name="TicketId"/> is optional: omitting it (or the caller
/// lacking <c>tickets.view</c>) simply leaves the ticket-scoped variables
/// (<c>TicketNumber</c>, <c>CustomerName</c>) unresolved rather than failing
/// the whole call (CRM-146).
/// </summary>
public sealed record ResolveQuickReplyRequest(Guid? TicketId);
