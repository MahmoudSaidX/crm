using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Modules.ArchitectureFixture.Contracts;

namespace SquadCrm.Modules.ArchitectureFixture;

/// <summary>
/// <b>Infrastructure/architecture scaffolding — not a CRM module.</b>
/// <para>
/// The minimum representative module fixture. Its only purpose is to prove
/// explicit module registration, the contracts-vs-implementation split, module
/// endpoint composition and the dependency rules asserted by
/// <c>SquadCrm.ArchitectureTests</c>.
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

    public string Name => ModuleName;

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Internal abstraction + internal implementation: the host composes the
        // module but cannot resolve or depend on the module's internals.
        services.AddSingleton<IModuleInfoProvider, ModuleInfoProvider>();
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
    }
}
