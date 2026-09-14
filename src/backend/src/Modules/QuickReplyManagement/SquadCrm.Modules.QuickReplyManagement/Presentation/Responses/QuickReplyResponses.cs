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
