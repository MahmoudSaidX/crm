using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.SystemConfiguration.Application.Services;
using SquadCrm.Modules.SystemConfiguration.Infrastructure.Persistence;

using SquadCrm.Modules.SystemConfiguration.Presentation.Endpoints;

namespace SquadCrm.Modules.SystemConfiguration;

public sealed class SystemConfigurationModule : IModule
{
    // The "configuration.view"/"configuration.manage" policies are centrally
    // owned and registered by RoleManagementModule (the permission catalog's
    // single home), following the same precedent as DepartmentManagementModule:
    // referenced here by string only, no project reference to RoleManagement.
    public string Name => "SystemConfiguration";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SystemConfigurationDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    SystemConfigurationSchema.MigrationsHistoryTable,
                    SystemConfigurationSchema.Name)));
        services.AddScoped<ConfigurationService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // DI resolves that same registration. No duplicate registration and
        // no project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        SystemConfigurationEndpoints.Map(endpoints);
}
