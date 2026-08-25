using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SquadCrm.BuildingBlocks.Modules;

/// <summary>
/// Module entry point. Implementations register their own services and
/// endpoints. The API host composes modules explicitly; business rules
/// never live on the host.
/// </summary>
public interface IModule
{
    /// <summary>Stable module identifier, used for diagnostics only.</summary>
    string Name { get; }

    /// <summary>Registers the module's own services. A module must not register
    /// services on behalf of another module.</summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>Maps the module's own endpoints onto the host's route table.</summary>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
