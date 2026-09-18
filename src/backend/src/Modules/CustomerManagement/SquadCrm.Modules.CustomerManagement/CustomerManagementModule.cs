using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.CustomerManagement.Application.Services;
using SquadCrm.Modules.CustomerManagement.Contracts;
using SquadCrm.Modules.CustomerManagement.Infrastructure.Persistence;

using SquadCrm.Modules.CustomerManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.CustomerManagement;

public sealed class CustomerManagementModule : IModule
{
    // The "customers.manage" policy is centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), following
    // the same precedent as AuditModule's AuditViewPolicy: referenced here by
    // string only, no project reference to RoleManagement.
    public string Name => "CustomerManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CustomerManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    CustomerManagementSchema.MigrationsHistoryTable,
                    CustomerManagementSchema.Name)));
        services.AddScoped<CustomerService>();
        services.AddScoped<CustomerContactService>();
        services.AddScoped<CustomerNoteService>();
        services.AddScoped<CustomerAttachmentService>();
        services.AddScoped<CustomerTimelineService>();
        services.AddScoped<ICustomerExistsLookup, CustomerExistsLookup>();
        services.AddScoped<ICustomerNameReader, CustomerNameReader>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // IDepartmentActiveLookup/IBranchActiveLookup are already registered
        // by DepartmentManagementModule/BranchManagementModule; DI resolves
        // those same registrations. No duplicate registration and no project
        // reference to those modules' main projects is added here.
        // ICustomerExistsLookup is this module's own contract (mirrors
        // IDepartmentActiveLookup/IBranchActiveLookup) — TicketManagement
        // consumes it via a project reference to this module's own
        // .Contracts project only, never this module's implementation.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        CustomerEndpoints.Map(endpoints);
}
