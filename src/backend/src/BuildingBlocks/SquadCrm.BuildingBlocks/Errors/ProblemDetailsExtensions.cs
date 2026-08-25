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
