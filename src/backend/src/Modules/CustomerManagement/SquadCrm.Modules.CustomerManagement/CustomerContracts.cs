using System.ComponentModel.DataAnnotations;
using SquadCrm.Modules.CustomerManagement.Persistence;

namespace SquadCrm.Modules.CustomerManagement;

public sealed record CreateCustomerRequest(
    [property: Required, MaxLength(200)] string FirstName,
    [property: Required, MaxLength(200)] string LastName,
    CustomerPreferredLanguage? PreferredLanguage,
    Guid? DepartmentId,
    Guid? BranchId);

public sealed record CustomerResponse(
    Guid Id,
    string CustomerNumber,
    string FirstName,
    string LastName,
    CustomerPreferredLanguage? PreferredLanguage,
    Guid? DepartmentId,
    Guid? BranchId,
    CustomerStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
