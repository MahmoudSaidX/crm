using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SquadCrm.Modules.CustomerManagement.Domain.Entities;

namespace SquadCrm.Modules.CustomerManagement.Presentation.Requests;

public enum CustomerSortBy
{
    CustomerNumber,
    FirstName,
    LastName,
    CreatedAtUtc,
}

public enum SortDirection
{
    Asc,
    Desc,
}

/// <summary>
/// Bound via <c>[AsParameters]</c> alongside <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/>.
/// A positional record, for the same minimal-API binder reason documented on
/// <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/>.
/// </summary>
public sealed record CustomerListQuery(
    string? Search = null,
    Guid[]? DepartmentIds = null,
    Guid[]? BranchIds = null,
    CustomerStatus[]? Status = null,
    CustomerSortBy SortBy = CustomerSortBy.CustomerNumber,
    SortDirection SortDirection = SortDirection.Asc);

public sealed record CreateCustomerRequest(
    [property: Required, MaxLength(200)] string FirstName,
    [property: Required, MaxLength(200)] string LastName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerPreferredLanguage? PreferredLanguage,
    Guid? DepartmentId,
    Guid? BranchId);

public sealed record UpdateCustomerRequest(
    [property: Required, MaxLength(200)] string FirstName,
    [property: Required, MaxLength(200)] string LastName,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerPreferredLanguage? PreferredLanguage,
    Guid? DepartmentId,
    Guid? BranchId,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerStatus Status,
    uint Version);

public sealed record AddCustomerContactRequest(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] CustomerContactType Type,
    [property: Required, MaxLength(320)] string Value,
    [property: MaxLength(100)] string? Label,
    bool IsPrimary);

public sealed record UpdateCustomerContactRequest(
    [property: Required, MaxLength(320)] string Value,
    [property: MaxLength(100)] string? Label,
    bool IsPrimary);

public sealed record DeactivateCustomerContactRequest(Guid? NewPrimaryContactId);

public sealed record AddCustomerNoteRequest([property: Required, MaxLength(4000)] string Body);
