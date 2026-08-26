using System.ComponentModel.DataAnnotations;

namespace SquadCrm.BuildingBlocks.Http;

/// <summary>
/// Standard bounded pagination request. Bind this via <c>[AsParameters]</c> (it is
/// a reference type — binding it without <c>[AsParameters]</c> on a GET endpoint
/// makes minimal APIs treat it as a JSON body, which fails) on any module endpoint
/// accepting <c>page</c>/<c>pageSize</c> query parameters, so
/// <see cref="SquadCrm.BuildingBlocks.Validation.ValidationEndpointFilter{TArgument}"/>
/// enforces the bounds uniformly.
/// <para>
/// <b>Implementation correction:</b> declared as a positional <c>record</c>, not a
/// class with property initializers. Minimal APIs' <c>[AsParameters]</c> binding
/// only reads default values from a public constructor's parameter metadata
/// (<c>System.Reflection.ParameterInfo</c>); a settable-property initializer (<c>{ get; init; } = 1;</c>) is
/// invisible to that binder, so omitting <c>page</c>/<c>pageSize</c> from the query
/// string threw <c>BadHttpRequestException</c> ("Required parameter ... was not
/// provided") instead of applying the default. A positional record's constructor
/// parameter defaults are honoured by the binder, so <c>Page</c>/<c>PageSize</c> are
/// genuinely optional query parameters.
/// </para>
/// </summary>
public sealed record PaginationRequest(
    [property: Range(1, int.MaxValue, ErrorMessage = "Page must be 1 or greater.")]
    int Page = 1,
    [property: Range(1, 200, ErrorMessage = "PageSize must be between 1 and 200.")]
    int PageSize = 20)
{
    /// <summary>
    /// <c>static readonly</c>, not <c>const</c>: a <c>const</c> would inline the
    /// literal into every consumer assembly at compile time instead of being
    /// resolved from this assembly at runtime.
    /// </summary>
    public static readonly int MaxPageSize = 200;
}
