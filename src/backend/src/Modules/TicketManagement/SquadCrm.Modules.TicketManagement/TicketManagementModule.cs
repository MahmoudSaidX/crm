using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.TicketManagement.Application.Services;
using SquadCrm.Modules.TicketManagement.Contracts;
using SquadCrm.Modules.TicketManagement.Infrastructure.Outbox;
using SquadCrm.Modules.TicketManagement.Infrastructure.Persistence;

using SquadCrm.Modules.TicketManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.TicketManagement;

public sealed class TicketManagementModule : IModule
{
    // The "ticketcategories.view"/"ticketcategories.manage" policies are
    // centrally owned and registered by RoleManagementModule (the permission
    // catalog's single home), following the same precedent as
    // DepartmentManagementModule: referenced here by string only, no project
    // reference to RoleManagement.
    public string Name => "TicketManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // ICorrelationIdAccessor is resolved from the container the HOST
        // registers it in (Program.cs) — this module never registers
        // IHttpContextAccessor itself; a module's persistence must not depend
        // on HttpContext directly (CRM-198, B2). The outbox interceptor is
        // wired inline here (a single interceptor does not warrant a separate
        // `*Options.Apply` helper, unlike ArchitectureFixtureDbContextOptions).
        services.AddDbContext<TicketManagementDbContext>((serviceProvider, options) =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    TicketManagementSchema.MigrationsHistoryTable,
                    TicketManagementSchema.Name))
                .AddInterceptors(new TicketManagementOutboxInterceptor(
                    serviceProvider.GetRequiredService<ICorrelationIdAccessor>())));
        services.AddScoped<TicketCategoryService>();
        services.AddScoped<TicketPriorityService>();
        services.AddScoped<TicketService>();
        services.AddScoped<TicketTimelineService>();
        services.AddScoped<TicketCollaborationService>();
        services.AddScoped<ITicketExistsLookup, TicketExistsLookup>();
        services.AddScoped<ITicketReferenceReader, TicketReferenceReader>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // IDepartmentActiveLookup/IBranchActiveLookup are already registered
        // by DepartmentManagementModule/BranchManagementModule;
        // ICustomerExistsLookup is already registered by
        // CustomerManagementModule; DI resolves those same registrations. No
        // duplicate registration and no project reference to those modules'
        // main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        TicketCategoryEndpoints.Map(endpoints);
        TicketPriorityEndpoints.Map(endpoints);
        TicketEndpoints.Map(endpoints);
    }
}
