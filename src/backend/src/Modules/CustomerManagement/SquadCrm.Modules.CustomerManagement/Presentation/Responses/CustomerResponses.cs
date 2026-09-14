using System.Text.Json.Serialization;
using SquadCrm.Modules.CustomerManagement.Domain.Entities;

namespace SquadCrm.Modules.CustomerManagement.Presentation.Responses;

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string FirstName,
    string LastName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerPreferredLanguage? PreferredLanguage,
    Guid? DepartmentId,
    Guid? BranchId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerStatus Status,
    uint Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CustomerContactResponse(
    Guid Id,
    Guid CustomerId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerContactType Type,
    string Value,
    string? Label,
    bool IsPrimary,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CustomerNoteResponse(
    Guid Id,
    Guid CustomerId,
    string Body,
    Guid AuthorUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record CustomerAttachmentResponse(
    Guid Id,
    Guid CustomerId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string? Description,
    string UploadedBy,
    DateTimeOffset UploadedAtUtc);

public enum CustomerTimelineVisibility
{
    Internal,
    Customer,
}

public sealed record CustomerTimelineEntryResponse(
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string? ActorDisplay,
    string RelatedEntityType,
    Guid RelatedEntityId,
    string Summary,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerTimelineVisibility Visibility);
