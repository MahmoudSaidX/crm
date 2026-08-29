namespace SquadCrm.BuildingBlocks.Correlation;

/// <summary>
/// Reads the current request's correlation id without any consumer depending
/// on <c>HttpContext</c> directly. Registered in the host composition root;
/// a module's persistence layer injects this interface, never
/// <c>IHttpContextAccessor</c> (CLAUDE.md: providers/cross-cutting concerns
/// stay behind provider-neutral ports).
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    /// The current <see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/>
    /// when one is available; otherwise a freshly generated id, matching
    /// <see cref="CorrelationIdMiddleware"/>'s own no-request fallback shape.
    /// Never longer than <see cref="CorrelationIdMiddleware.MaxLength"/>.
    /// </summary>
    string Current { get; }
}
