using Microsoft.AspNetCore.Http;

namespace SquadCrm.BuildingBlocks.Correlation;

/// <summary>
/// <c>public</c> only because DI registration in <c>Program.cs</c> (a
/// different assembly) must reference this concrete type to wire it up.
/// Modules must depend on and inject <see cref="ICorrelationIdAccessor"/>,
/// never this concrete class directly.
/// </summary>
public sealed class HttpContextCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
    : ICorrelationIdAccessor
{
    public string Current =>
        httpContextAccessor.HttpContext?.TraceIdentifier is { Length: > 0 } traceId
            ? traceId
            : Guid.NewGuid().ToString("n");
}
