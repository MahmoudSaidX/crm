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
/// <para>
/// <b>One exception is not a server fault.</b> ASP.NET Core raises
/// <see cref="BadHttpRequestException"/> when it cannot bind a request —
/// <c>?page=abc</c>, an unknown enum member, a malformed <c>Guid</c> filter —
/// and that exception already carries its own <c>400</c> status. Treating it
/// as a <c>500</c> reported the caller's typo as a server outage: it polluted
/// error budgets and alerting, and told a client to retry something that will
/// never succeed. Its status is now honoured. The message still never reaches
/// the body (it can quote the offending value back), and it is logged at
/// warning rather than error.
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

    /// <summary>Client-error counterparts, used only for a failed request binding.</summary>
    private const string InvalidRequestTitle = "The request could not be understood.";
    private const string InvalidRequestType = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1";
    private const string InvalidRequestCode = "invalid-request";

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

        if (exception is BadHttpRequestException badRequest)
        {
            _logger.LogWarning(
                badRequest,
                "Rejected a malformed request. traceId={TraceId}",
                traceId);

            httpContext.Response.StatusCode = badRequest.StatusCode;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = new ProblemDetails
                {
                    Type = InvalidRequestType,
                    Title = InvalidRequestTitle,
                    Status = badRequest.StatusCode,
                    Instance = httpContext.Request.Path.Value,
                    Extensions = { [ProblemDetailsExtensions.CodeExtensionName] = InvalidRequestCode },
                },
            }).ConfigureAwait(false);
        }

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
