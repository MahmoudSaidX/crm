using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.SystemConfiguration.Application.Services;
using SquadCrm.Modules.SystemConfiguration.Presentation.Requests;
using SquadCrm.Modules.SystemConfiguration.Presentation.Responses;

namespace SquadCrm.Modules.SystemConfiguration.Presentation.Endpoints;

/// <summary>
/// HTTP surface of the SystemConfiguration module: route definitions, model binding,
/// status mapping and response projection. Composed by
/// <see cref="SystemConfigurationModule"/>, which owns registration only.
/// </summary>
internal static class SystemConfigurationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
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
