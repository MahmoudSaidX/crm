using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace SquadCrm.BuildingBlocks.Security;

/// <summary>
/// Adds a conventional, low-risk response header baseline to every response,
/// including error responses (registered right after
/// <c>app.UseExceptionHandler()</c> — see <c>Program.cs</c> — so a 500 carries
/// the headers too).
/// <para>
/// <see cref="StrictTransportSecurityValue"/> is added only when
/// <see cref="IHostEnvironment.IsProduction"/> is <see langword="true"/> —
/// deliberately <b>not</b> gated on <c>HttpContext.Request.IsHttps</c>. Behind
/// a TLS-terminating proxy (the expected production topology) the inbound
/// request to Kestrel is plain HTTP even though the client connection was
/// HTTPS, so <c>Request.IsHttps</c> would be <see langword="false"/> and HSTS
/// would silently never fire in production if gated that way.
/// </para>
/// <para>
/// Deliberately excluded: <c>Content-Security-Policy</c> and
/// <c>frame-ancestors</c> — their correct value depends on the not-yet-built
/// frontend/deployment topology (open item; see the CRM-204 intake).
/// </para>
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string StrictTransportSecurityValue = "max-age=31536000; includeSubDomains";

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(environment);
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        bool isProduction = _environment.IsProduction();

        context.Response.OnStarting(state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
            ctx.Response.Headers["X-Frame-Options"] = "DENY";
            ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            if (isProduction)
            {
                ctx.Response.Headers["Strict-Transport-Security"] = StrictTransportSecurityValue;
            }

            return Task.CompletedTask;
        }, context);

        await _next(context).ConfigureAwait(false);
    }
}
