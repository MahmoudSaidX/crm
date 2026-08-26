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
/// The CORS allow-list is not overridden by default: tests exercise the
/// configuration the application actually ships. Development supplies
/// <c>http://localhost:4200</c> via <c>appsettings.Development.json</c>; every other
/// environment falls back to the empty allow-list in <c>appsettings.json</c>.
/// <see cref="WithEmptyCorsAllowList"/> is the one exception — see below.
/// </para>
/// <para>
/// <b>Why an empty-allow-list override exists.</b> Environment variables override
/// <c>appsettings</c>, by design and in real deployments. Since CRM-106 the
/// documented <c>dotnet test</c> workflow loads <c>env/backend.env</c> into the
/// shell (the persistence suite needs the <c>POSTGRES_*</c> values), and that file
/// also carries <c>CORS__AllowedOrigins__0</c> for the containerised app. A test
/// whose subject is "an empty allow-list blocks everything" must state that
/// allow-list itself rather than inherit whichever value the developer's shell
/// happens to hold. The tests that prove the shipped Development configuration
/// flows through are untouched and still read it from <c>appsettings</c>.
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
/// <para>
/// <b>Database configuration:</b> the host validates the <c>POSTGRES_*</c>
/// contract fail-fast at startup (CRM-106), so this factory supplies obviously
/// fake placeholder values. They let the host start with <b>no database
/// running</b> — no connection is ever opened by these tests, so the values are
/// never used to reach a server. They are deliberately not real credentials and
/// must never become any. Removing this block makes every test here fail at
/// startup; that failure is the intended signal, not a reason to weaken the
/// host's validation. Real database behaviour is proven by
/// <c>SquadCrm.Persistence.IntegrationTests</c> against a real server.
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

    /// <summary>
    /// Obviously fake, non-secret placeholder password. Never a real credential:
    /// no connection is opened by the API test suite.
    /// </summary>
    public const string PlaceholderPassword = "not-a-real-password";

    private readonly string _environment;
    private readonly bool _injectFault;
    private readonly bool _emptyCorsAllowList;

    public SquadCrmApiFactory()
        : this(Environments.Development)
    {
    }

    public SquadCrmApiFactory(string environment, bool injectFault = false)
    {
        _environment = environment;
        _injectFault = injectFault;
    }

    private SquadCrmApiFactory(string environment, bool injectFault, bool emptyCorsAllowList)
        : this(environment, injectFault)
    {
        _emptyCorsAllowList = emptyCorsAllowList;
    }

    /// <summary>
    /// A host whose CORS allow-list is explicitly empty, regardless of ambient
    /// configuration. Use only for the "empty allow-list blocks everything" case.
    /// </summary>
    public static SquadCrmApiFactory WithEmptyCorsAllowList(string environment) =>
        new(environment, injectFault: false, emptyCorsAllowList: true);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(_environment);

        // Satisfies the host's fail-fast POSTGRES_* validation without a server.
        // UseSetting, not ConfigureAppConfiguration: under minimal hosting the
        // host-configuration callbacks run *after* Program's top-level statements,
        // so an in-memory source added there would arrive too late for the
        // AddSquadCrmPostgres call that validates at composition time.
        builder.UseSetting("POSTGRES_HOST", "localhost");
        builder.UseSetting("POSTGRES_PORT", "5432");
        builder.UseSetting("POSTGRES_DB", "squadcrm-tests");
        builder.UseSetting("POSTGRES_USER", "squadcrm-tests");
        builder.UseSetting("POSTGRES_PASSWORD", PlaceholderPassword);

        if (_emptyCorsAllowList)
        {
            // Clears index 0 rather than "setting an empty array": configuration
            // arrays have no removal primitive, and index 0 is the only element
            // any shipped or environment source defines.
            // The key is written out because SquadCrm.Api's CorsOptions is
            // internal, and a test is not a reason to widen production
            // visibility. It is covered by the two tests that read the shipped
            // Development allow-list: if the section were renamed, they fail.
            builder.UseSetting("Cors:AllowedOrigins:0", null);
        }

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
