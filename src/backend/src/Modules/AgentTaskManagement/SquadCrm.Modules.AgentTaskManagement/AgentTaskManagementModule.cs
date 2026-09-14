using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Correlation;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.AgentTaskManagement.Application.Services;
using SquadCrm.Modules.AgentTaskManagement.Infrastructure.BackgroundProcessing;
using SquadCrm.Modules.AgentTaskManagement.Infrastructure.Outbox;
using SquadCrm.Modules.AgentTaskManagement.Infrastructure.Persistence;

using SquadCrm.Modules.AgentTaskManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.AgentTaskManagement;

public sealed class AgentTaskManagementModule : IModule
{
    // The "tasks.*" policies are centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), same
    // precedent as TicketManagement: referenced here by string only, no
    // project reference to RoleManagement.
    public string Name => "AgentTaskManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // ICorrelationIdAccessor is resolved from the container the HOST
        // registers it in (Program.cs) — mirrors TicketManagementModule.
        services.AddDbContext<AgentTaskManagementDbContext>((serviceProvider, options) =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    AgentTaskManagementSchema.MigrationsHistoryTable,
                    AgentTaskManagementSchema.Name))
                .AddInterceptors(new AgentTaskManagementOutboxInterceptor(
                    serviceProvider.GetRequiredService<ICorrelationIdAccessor>())));
        services.AddScoped<AgentTaskService>();

        // The due-reminder sweep (CRM-144). Scoped like every other service
        // here: Hangfire resolves the job inside its own per-execution scope.
        services.AddScoped<AgentTaskReminderService>();
        services.AddScoped<AgentTaskReminderJob>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule;
        // ICustomerExistsLookup by CustomerManagementModule; ITicketExistsLookup
        // by TicketManagementModule; IStaffSubjectReferenceReader by
        // StaffIdentityModule. DI resolves those same registrations. No
        // duplicate registration and no project reference to those modules'
        // main projects is added here.
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        AgentTaskManagementEndpoints.Map(endpoints);
}
