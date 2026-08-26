using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SquadCrm.BuildingBlocks.Errors;

/// <summary>
/// Registers ASP.NET Core's built-in <c>ProblemDetails</c> services and
/// customises every produced document to the RFC 9457 shape used across the API.
/// <para>
/// Every response carries <c>type</c>, <c>title</c>, <c>status</c>, <c>instance</c>
/// and a lowercase <c>traceId</c> extension. Stack traces, exception messages and
/// inner-exception content are never added here, in any environment.
/// </para>
/// </summary>
public static class ProblemDetailsExtensions
{
    /// <summary>Name of the correlation/trace extension member. Always lowercase <c>traceId</c>.</summary>
    public const string TraceIdExtensionName = "traceId";

    /// <summary>
    /// Name of the stable, machine-readable error-code extension member. Always
    /// lowercase <c>code</c>. Each module declares and owns its own code values
    /// under its own naming convention (documented in docs/api-conventions.md);
    /// this building block owns only the wire format and plumbing — it is not a
    /// registry of every module's codes.
    /// </summary>
    public const string CodeExtensionName = "code";

    /// <summary>
    /// Name of the client/support correlation-handle extension member. Always
    /// lowercase <c>correlationId</c>. Sourced from <c>CorrelationIdMiddleware</c>'s
    /// promoted value (<see cref="Microsoft.AspNetCore.Http.HttpContext.TraceIdentifier"/>
    /// at the point the middleware set it) — <b>distinct from <see cref="TraceIdExtensionName"/></b>,
    /// which prefers the ambient <see cref="Activity"/> id. The two are never
    /// guaranteed equal; they diverge whenever an <see cref="Activity"/> is active.
    /// </summary>
    public const string CorrelationIdExtensionName = "correlationId";

    /// <summary>
    /// Adds Problem Details services configured for the Squad CRM error contract.
    /// This is the single place the contract is shaped; handlers and filters only
    /// supply <c>status</c>/<c>title</c>/<c>errors</c>.
    /// </summary>
    public static IServiceCollection AddSquadCrmProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.Value;
                context.ProblemDetails.Extensions[TraceIdExtensionName] = ResolveTraceId(context.HttpContext);

                // CorrelationIdMiddleware promotes the sanitised inbound/generated
                // correlation id onto HttpContext.TraceIdentifier before the rest
                // of the pipeline runs, unconditionally (not only when an
                // Activity is active) — unlike ResolveTraceId, which prefers
                // Activity.Current?.Id. Reading TraceIdentifier directly here
                // keeps correlationId sourced from the middleware's value and
                // deliberately distinct from traceId once an Activity exists.
                context.ProblemDetails.Extensions[CorrelationIdExtensionName] =
                    context.HttpContext.TraceIdentifier;
            };
        });

        return services;
    }

    /// <summary>
    /// Resolves the safe trace identifier: the ambient <see cref="Activity"/> id when
    /// one exists, otherwise the request's <see cref="HttpContext.TraceIdentifier"/>
    /// (which <c>CorrelationIdMiddleware</c> has already sanitised).
    /// </summary>
    public static string ResolveTraceId(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }
}
