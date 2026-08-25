using Microsoft.AspNetCore.Http;

namespace SquadCrm.BuildingBlocks.Correlation;

/// <summary>
/// Reads an inbound <c>X-Correlation-Id</c>, sanitises it, promotes it to
/// <see cref="HttpContext.TraceIdentifier"/> and echoes it on the response.
/// <para>
/// A client-supplied value is never trusted verbatim: values that are empty,
/// longer than <see cref="MaxLength"/> characters, or that contain control
/// characters are replaced by a freshly generated identifier. Arbitrary client
/// headers are never logged.
/// </para>
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>Inbound/outbound correlation header name.</summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>Maximum accepted length of a client-supplied correlation id.</summary>
    public const int MaxLength = 128;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string correlationId = Sanitise(context.Request.Headers[HeaderName]);
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers[HeaderName] = ctx.TraceIdentifier;
            return Task.CompletedTask;
        }, context);

        await _next(context).ConfigureAwait(false);
    }

    private static string Sanitise(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > MaxLength)
        {
            return Generate();
        }

        foreach (char character in candidate)
        {
            if (char.IsControl(character))
            {
                return Generate();
            }
        }

        return candidate;
    }

    private static string Generate() => Guid.NewGuid().ToString("n");
}
