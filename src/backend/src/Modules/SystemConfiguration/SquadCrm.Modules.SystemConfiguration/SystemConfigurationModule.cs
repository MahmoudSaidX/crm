using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.SystemConfiguration.Persistence;

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

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder configuration = endpoints.MapGroup("/api/v1/system-configuration").WithTags("SystemConfiguration");

        configuration.MapGet("", ListAsync).RequireAuthorization(PermissionPolicies.ConfigurationView);
        configuration.MapPut("/{key}", UpdateAsync).ValidatesDataAnnotations<UpdateConfigurationValueRequest>()
            .RequireAuthorization(PermissionPolicies.ConfigurationManage);
    }

    private static async Task<IResult> ListAsync(ConfigurationService configurationService, CancellationToken cancellationToken)
    {
        IReadOnlyList<ConfigurationValueResponse> values = await configurationService.ListAsync(cancellationToken);
        return Results.Ok(values);
    }

    private static async Task<IResult> UpdateAsync(
        string key,
        UpdateConfigurationValueRequest request,
        ConfigurationService configurationService,
        CancellationToken cancellationToken)
    {
        ConfigurationUpdateResult result = await configurationService.UpdateAsync(key, request, cancellationToken);
        return result.Failure switch
        {
            ConfigurationUpdateFailure.None => Results.Ok(result.Value),
            ConfigurationUpdateFailure.NotFound => NotFoundProblem(),
            ConfigurationUpdateFailure.InvalidValue => InvalidValueProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Configuration key not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "configuration.not_found" });

    private static IResult InvalidValueProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "The provided value is not valid for this configuration key's type/range.",
        extensions: new Dictionary<string, object?> { ["code"] = "configuration.invalid_value" });
}
