using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.BranchManagement.Application.Services;
using SquadCrm.Modules.BranchManagement.Contracts;
using SquadCrm.Modules.BranchManagement.Infrastructure.Persistence;

using SquadCrm.Modules.BranchManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.BranchManagement;

public sealed class BranchManagementModule : IModule
{
    // The "branches.view"/"branches.manage" policies are centrally
    // owned and registered by RoleManagementModule (the permission catalog's
    // single home), following the same precedent as AuditModule's
    // AuditViewPolicy: referenced here by string only, no project reference
    // to RoleManagement.
    public string Name => "BranchManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BranchManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    BranchManagementSchema.MigrationsHistoryTable,
                    BranchManagementSchema.Name)));
        services.AddScoped<BranchService>();
        services.AddScoped<IBranchActiveLookup, BranchActiveLookup>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // DI resolves that same registration. No duplicate registration and
        // no project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        BranchManagementEndpoints.Map(endpoints);
}
