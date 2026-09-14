using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.BrandingManagement.Application.Services;
using SquadCrm.Modules.BrandingManagement.Infrastructure.Persistence;

using SquadCrm.Modules.BrandingManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.BrandingManagement;

public sealed class BrandingManagementModule : IModule
{
    // The "branding.view"/"branding.manage" policies are centrally owned and
    // registered by RoleManagementModule (the permission catalog's single
    // home), following the same precedent as DepartmentManagementModule:
    // referenced here by string only, no project reference to RoleManagement.
    public string Name => "BrandingManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BrandingManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    BrandingManagementSchema.MigrationsHistoryTable,
                    BrandingManagementSchema.Name)));
        services.AddScoped<BrandingService>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule
        // and IFileStorage by the composition root; DI resolves those same
        // registrations. No duplicate registration and no project reference
        // to StaffIdentity/FileStorage is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        BrandingManagementEndpoints.Map(endpoints);
}
