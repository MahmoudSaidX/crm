using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SquadCrm.Api.Tests;

/// <summary>
/// Hosts <c>SquadCrm.Api</c> in-memory. The environment is overridable per test so
/// environment-gated and configuration-driven behaviour can be exercised.
/// <para>
/// The CORS allow-list is not overridden: tests exercise the configuration the
/// application actually ships. Development supplies
/// <c>http://localhost:4200</c> via <c>appsettings.Development.json</c>; every other
/// environment falls back to the empty allow-list in <c>appsettings.json</c>.
/// </para>
/// <para>
/// <b>Fault injection:</b> to exercise the error contract, the factory can replace
/// <see cref="ICorsPolicyProvider"/> with one that throws. The fault is raised by a
/// service the host's <i>own</i> middleware resolves inside the pipeline guarded by
/// <c>UseExceptionHandler</c>, so the assertion covers the real host. This DI seam
/// is used instead of a test-only route because <c>WebApplication</c>'s route table
/// cannot be extended from outside the host. Production code therefore contains no
/// throw/debug endpoint of any kind.
/// </para>
/// </summary>
public sealed class SquadCrmApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Path used by the error-contract tests. It is never routed; the fault is raised first.</summary>
    public const string FaultRoute = "/test-only/throw";

    /// <summary>Exception message that must never reach the response body.</summary>
    public const string SentinelMessage = "SENTINEL-EXCEPTION-MESSAGE";

    /// <summary>Inner-exception message that must never reach the response body.</summary>
    public const string SentinelInnerMessage = "SENTINEL-INNER-MESSAGE";

    private readonly string _environment;
    private readonly bool _injectFault;

    public SquadCrmApiFactory()
        : this(Environments.Development)
    {
    }

    public SquadCrmApiFactory(string environment, bool injectFault = false)
    {
        _environment = environment;
        _injectFault = injectFault;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(_environment);

        if (_injectFault)
        {
            builder.ConfigureTestServices(services =>
                services.AddSingleton<ICorsPolicyProvider, ThrowingCorsPolicyProvider>());
        }
    }

    /// <summary>Raises an unhandled exception from inside the host's request pipeline.</summary>
    private sealed class ThrowingCorsPolicyProvider : ICorsPolicyProvider
    {
        public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName) =>
            throw new InvalidOperationException(
                SentinelMessage,
                new InvalidOperationException(SentinelInnerMessage));
    }
}
