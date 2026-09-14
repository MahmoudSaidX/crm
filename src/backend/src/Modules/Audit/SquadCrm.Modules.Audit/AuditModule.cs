using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.Audit.Application.Services;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Infrastructure.Persistence;

using SquadCrm.Modules.Audit.Presentation.Endpoints;

namespace SquadCrm.Modules.Audit;

public sealed class AuditModule : IModule
{

    public string Name => "Audit";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuditDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    AuditSchema.MigrationsHistoryTable,
                    AuditSchema.Name)));
        services.AddScoped<IAuditRecorder, AuditRecorder>();
        services.AddScoped<AuditQueryService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        AuditEndpoints.Map(endpoints);
}
