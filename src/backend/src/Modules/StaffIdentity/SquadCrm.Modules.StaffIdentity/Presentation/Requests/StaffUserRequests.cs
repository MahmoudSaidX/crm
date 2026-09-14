using System.ComponentModel.DataAnnotations;

namespace SquadCrm.Modules.StaffIdentity.Presentation.Requests;

public sealed record CreateStaffUserRequest(
    [property: Required, EmailAddress, MaxLength(320)] string Email,
    [property: Required, MinLength(8), MaxLength(200)] string Password,
    [property: MaxLength(200)] string? DisplayName,
    [property: MaxLength(200)] string? Department,
    [property: MaxLength(200)] string? Branch);

public sealed record UpdateStaffUserRequest(
    [property: MaxLength(200)] string? DisplayName,
    [property: MaxLength(200)] string? Department,
    [property: MaxLength(200)] string? Branch);

/// <summary>
/// Staff-user list filter, bound via <c>[AsParameters]</c> alongside
/// <see cref="SquadCrm.BuildingBlocks.Http.PaginationRequest"/>.
/// <para>
/// Was a loose <c>string? search</c> handler parameter that no filter could
/// validate. Same query-string name, same behaviour within the bound; an
/// over-long term is now a 400 rather than an unbounded scan pattern. The term
/// itself is never filtered or rewritten — it reaches EF Core as a parameter.
/// </para>
/// </summary>
public sealed record StaffUserListQuery([property: MaxLength(200)] string? Search = null);
