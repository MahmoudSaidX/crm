using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SquadCrm.BuildingBlocks.Modules;

/// <summary>
/// Composes an <b>explicitly supplied</b> set of modules.
/// <para>
/// There is deliberately no runtime assembly scanning: the host lists its
/// modules in code, so a module that is not registered is a compile error
/// rather than a silent runtime gap.
/// </para>
/// </summary>
public static class ModuleRegistrar
{
    /// <summary>Invokes <see cref="IModule.RegisterServices"/> for each supplied module.</summary>
    public static IServiceCollection RegisterModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IReadOnlyCollection<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (IModule module in modules)
        {
            module.RegisterServices(services, configuration);
        }

        return services;
    }

    /// <summary>Invokes <see cref="IModule.MapEndpoints"/> for each supplied module.</summary>
    public static IEndpointRouteBuilder MapModuleEndpoints(
        this IEndpointRouteBuilder endpoints,
        IReadOnlyCollection<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (IModule module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
