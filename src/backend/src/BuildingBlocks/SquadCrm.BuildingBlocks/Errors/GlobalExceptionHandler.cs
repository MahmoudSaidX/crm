using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace SquadCrm.BuildingBlocks.Errors;

/// <summary>
/// Terminal handler for anything that escapes the pipeline. Every unexpected
/// exception becomes a generic RFC 9457 <c>500</c> document carrying only a
/// <c>traceId</c>.
/// <para>
/// The exception message, stack trace and inner-exception chain are written to
/// the log (server side) and never to the response body, in any environment —
/// the log is correlated to the caller through <c>traceId</c>.
/// </para>
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private const string GenericTitle = "An unexpected error occurred.";
    private const string ProblemType = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1";

    /// <summary>
    /// Fixed, generic code for every unhandled exception — not module-owned, since
    /// this handler runs before any module-specific error is distinguishable.
    /// </summary>
    private const string GenericCode = "unexpected-error";

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(logger);
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The response is already on the wire; rewriting it would corrupt it.
        // Returning false lets the server abort the connection instead.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        string traceId = ProblemDetailsExtensions.ResolveTraceId(httpContext);

        _logger.LogError(
            exception,
            "Unhandled exception while processing the request. traceId={TraceId}",
            traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Type = ProblemType,
                Title = GenericTitle,
                Status = StatusCodes.Status500InternalServerError,
                Instance = httpContext.Request.Path.Value,
                Extensions = { [ProblemDetailsExtensions.CodeExtensionName] = GenericCode },
            },
        }).ConfigureAwait(false);
    }
}
