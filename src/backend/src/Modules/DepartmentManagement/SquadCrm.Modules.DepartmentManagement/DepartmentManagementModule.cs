using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.DepartmentManagement.Application.Services;
using SquadCrm.Modules.DepartmentManagement.Contracts;
using SquadCrm.Modules.DepartmentManagement.Infrastructure.Persistence;

using SquadCrm.Modules.DepartmentManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.DepartmentManagement;

public sealed class DepartmentManagementModule : IModule
{
    // The "departments.view"/"departments.manage" policies are centrally
    // owned and registered by RoleManagementModule (the permission catalog's
    // single home), following the same precedent as AuditModule's
    // AuditViewPolicy: referenced here by string only, no project reference
    // to RoleManagement.
    public string Name => "DepartmentManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DepartmentManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    DepartmentManagementSchema.MigrationsHistoryTable,
                    DepartmentManagementSchema.Name)));
        services.AddScoped<DepartmentService>();
        services.AddScoped<IDepartmentActiveLookup, DepartmentActiveLookup>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // DI resolves that same registration. No duplicate registration and
        // no project reference to StaffIdentity is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        DepartmentManagementEndpoints.Map(endpoints);
}
