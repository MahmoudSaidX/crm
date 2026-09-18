using System.Text.Json.Serialization;
using SquadCrm.Modules.QuickReplyManagement.Domain.Entities;

namespace SquadCrm.Modules.QuickReplyManagement.Presentation.Responses;

public sealed record QuickReplyResponse(
    Guid Id,
    string Name,
    string? ArabicContent,
    string? EnglishContent,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] QuickReplyScope Scope,
    Guid? OwnerUserId,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>
/// The allow-listed variables (<c>{{CustomerName}}</c>, <c>{{TicketNumber}}</c>,
/// <c>{{AgentName}}</c>) substituted where resolvable. A name in
/// <paramref name="UnresolvedVariables"/> was left as its literal
/// <c>{{Token}}</c> text in the content above — never blanked, never
/// silently dropped (CRM-146 AC: "surfaced safely").
/// </summary>
public sealed record ResolvedQuickReplyResponse(
    string? ArabicContent,
    string? EnglishContent,
    IReadOnlyList<string> UnresolvedVariables);
