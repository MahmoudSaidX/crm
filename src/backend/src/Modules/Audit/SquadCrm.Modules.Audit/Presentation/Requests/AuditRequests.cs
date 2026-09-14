using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.Audit.Presentation.Requests;

/// <summary>
/// Audit-trail filters, bound via <c>[AsParameters]</c> alongside
/// <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/>.
/// <para>
/// Previously these were four loose <c>string?</c>/<c>DateTimeOffset?</c>
/// handler parameters, which no filter could validate. The wire contract is
/// unchanged — the query-string names are identical — but the values are now
/// length-bounded, because an unbounded filter string becomes an unbounded
/// <c>LIKE</c> pattern against the busiest table in the system.
/// </para>
/// <para>
/// The bound truncates nothing and strips nothing: a value that is too long is
/// rejected with a 400, and everything within the bound — apostrophes, Arabic,
/// any Unicode — reaches the query as data, parameterized by EF Core.
/// </para>
/// </summary>
public sealed record AuditListQuery(
    [property: MaxLength(200)] string? EntityType = null,
    [property: MaxLength(200)] string? Action = null,
    [property: MaxLength(200)] string? ActorHandle = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
