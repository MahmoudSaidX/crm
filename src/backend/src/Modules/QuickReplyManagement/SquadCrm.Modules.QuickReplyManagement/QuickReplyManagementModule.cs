using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.QuickReplyManagement.Application.Services;
using SquadCrm.Modules.QuickReplyManagement.Infrastructure.Authorization;
using SquadCrm.Modules.QuickReplyManagement.Infrastructure.Persistence;

using SquadCrm.Modules.QuickReplyManagement.Presentation.Endpoints;

namespace SquadCrm.Modules.QuickReplyManagement;

public sealed class QuickReplyManagementModule : IModule
{
    // The "quickreplies.*" policies are centrally owned and registered by
    // RoleManagementModule (the permission catalog's single home), same
    // precedent as DepartmentManagement: referenced here by string only, no
    // project reference to RoleManagement.
    public string Name => "QuickReplyManagement";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<QuickReplyManagementDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    QuickReplyManagementSchema.MigrationsHistoryTable,
                    QuickReplyManagementSchema.Name)));
        services.AddScoped<QuickReplyService>();
        services.AddScoped<IGlobalQuickReplyAuthorizer, GlobalQuickReplyAuthorizer>();

        // ICurrentUserAccessor is already registered by StaffIdentityModule and
        // IAuditRecorder by AuditModule; DI resolves those same registrations.
        // IHttpContextAccessor is registered by the host (Program.cs).
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        QuickReplyManagementEndpoints.Map(endpoints);
}
