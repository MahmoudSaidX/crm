using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.ArchitectureFixture.Contracts;
using SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Modules.ArchitectureFixture;

/// <summary>
/// <b>Infrastructure/architecture scaffolding — not a CRM module.</b>
/// <para>
/// The minimum representative module fixture. Its only purpose is to prove
/// explicit module registration, the contracts-vs-implementation split, module
/// endpoint composition and the dependency rules asserted by
/// <c>SquadCrm.ArchitectureTests</c>. It additionally proves <b>module-owned
/// persistence</b> — its own <see cref="ArchitectureFixtureDbContext"/>, schema,
/// table, migrations and migration-history table — and is removable on exactly
/// the same terms as the rest of the fixture, together with its schema and
/// migrations.
/// </para>
/// <para>
/// It must not grow into a business module, nor into a cross-cutting "Platform"
/// module — cross-cutting technical concerns belong in <c>SquadCrm.BuildingBlocks</c>.
/// It can and should be deleted once real business modules provide equivalent
/// architecture-rule coverage, at which point the architecture rules retarget to
/// the real module assemblies.
/// </para>
/// </summary>
public sealed class ArchitectureFixtureModule : IModule
{
    /// <summary>Non-business module identifier.</summary>
    public const string ModuleName = "ArchitectureFixture";

    /// <summary>Infrastructure/demo-only route. Not a product surface.</summary>
    public const string ModuleInfoRoute = "/internal/architecture-fixture/module-info";

    /// <summary>
    /// Infrastructure/demo-only route. Not a product surface. Proves the shared
    /// <see cref="PagedResult{TItem}"/> envelope and <see cref="ValidationEndpointFilter{TArgument}"/>
    /// end to end, under the <c>/api/v1</c> route-prefix convention (D5).
    /// </summary>
    public const string ModuleInfoPageRoute = "/api/v1/internal/architecture-fixture/module-info-page";

    public string Name => ModuleName;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Internal abstraction + internal implementation: the host composes the
        // module but cannot resolve or depend on the module's internals.
        services.AddSingleton<IModuleInfoProvider, ModuleInfoProvider>();

        // The module registers its OWN DbContext, using the connection string the
        // composition root derived from the POSTGRES_* contract. The host never
        // sees this type, and no other module may reference it
        // (SquadCrm.ArchitectureTests enforces this).
        // ICorrelationIdAccessor is resolved from the container the HOST
        // registers it in (Program.cs) — this module never registers
        // IHttpContextAccessor itself; a module's persistence must not depend
        // on HttpContext directly (CRM-198, B2).
        services.AddDbContext<ArchitectureFixtureDbContext>((serviceProvider, options) =>
            ArchitectureFixtureDbContextOptions.Apply(
                options,
                configuration.GetSquadCrmPostgresConnectionString(),
                serviceProvider.GetRequiredService<ICorrelationIdAccessor>()));

        services.AddOptions<OutboxProcessingOptions>()
            .Bind(configuration.GetSection(OutboxProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<ArchitectureFixtureOutboxStore>();
        services.AddScoped<ArchitectureFixtureIntegrationEventDispatcher>();
        services.AddScoped<ArchitectureFixtureOutboxJob>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Exactly one endpoint, taking no input and applying no business logic.
        // It exists solely to prove that module services and module routes are
        // reachable through the host.
        endpoints.MapGet(ModuleInfoRoute, static (IModuleInfoProvider provider) =>
                TypedResults.Ok<ModuleInfoResponse>(provider.Describe()))
            .WithName("ArchitectureFixtureModuleInfo")
            .WithSummary("Infrastructure/demo-only: confirms the architecture fixture module is registered.")
            .WithDescription(
                "Architecture scaffolding, not a CRM capability. Removed once real business "
                + "modules provide equivalent architecture-rule coverage.");

        // Proves ValidationEndpointFilter<T> attached to a real endpoint boundary
        // (CRM-105 declared the filter but attached it to no endpoint) and the
        // shared PagedResult<T> envelope, together, at a live boundary.
        // PaginationRequest is a reference type, so [AsParameters] is required —
        // without it, minimal APIs would try to bind this GET request as a JSON body.
        endpoints.MapGet(ModuleInfoPageRoute, static ([AsParameters] PaginationRequest request, IModuleInfoProvider provider) =>
                TypedResults.Ok(new PagedResult<ModuleInfoResponse>(
                    Items: [provider.Describe()],
                    Page: request.Page,
                    PageSize: request.PageSize,
                    TotalCount: 1)))
            .WithName("ArchitectureFixtureModuleInfoPage")
            .WithSummary("Infrastructure/demo-only: proves the shared PagedResult<T> envelope and ValidationEndpointFilter<T> end to end.")
            .WithDescription(
                "Architecture scaffolding, not a CRM capability. Removed once a real business "
                + "module's paged endpoint provides equivalent coverage.")
            .ValidatesDataAnnotations<PaginationRequest>();
    }
}
