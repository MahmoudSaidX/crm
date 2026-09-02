using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.BuildingBlocks.Abstractions.Files;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.BrandingManagement.Persistence;

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

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder branding = endpoints.MapGroup("/api/v1/branding").WithTags("Branding");

        branding.MapGet("", GetSettingsAsync).RequireAuthorization(PermissionPolicies.BrandingView);
        branding.MapPut("", UpdateAsync).ValidatesDataAnnotations<UpdateBrandingSettingsRequest>()
            .RequireAuthorization(PermissionPolicies.BrandingManage);
        branding.MapPost("/logo/{kind}", UploadLogoAsync)
            .RequireAuthorization(PermissionPolicies.BrandingManage)
            .DisableAntiforgery();
        branding.MapDelete("/logo/{kind}", DeleteLogoAsync).RequireAuthorization(PermissionPolicies.BrandingManage);

        branding.MapGet("/effective", GetEffectiveAsync).AllowAnonymous();
        branding.MapGet("/logo/{kind}", GetLogoAsync).AllowAnonymous();
    }

    private static async Task<IResult> GetSettingsAsync(BrandingService brandingService, CancellationToken cancellationToken) =>
        Results.Ok(await brandingService.GetSettingsAsync(cancellationToken));

    private static async Task<IResult> GetEffectiveAsync(BrandingService brandingService, CancellationToken cancellationToken) =>
        Results.Ok(await brandingService.GetEffectiveAsync(cancellationToken));

    private static async Task<IResult> UpdateAsync(
        UpdateBrandingSettingsRequest request, BrandingService brandingService, CancellationToken cancellationToken)
    {
        BrandingUpdateResult result = await brandingService.UpdateAsync(request, cancellationToken);
        return result.Failure switch
        {
            BrandingUpdateFailure.None => Results.Ok(await brandingService.GetSettingsAsync(cancellationToken)),
            BrandingUpdateFailure.InvalidThemeToken => InvalidThemeTokenProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> UploadLogoAsync(
        string kind, IFormFile file, ICurrentUserAccessor currentUserAccessor, BrandingService brandingService,
        CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out BrandingAssetKind assetKind))
        {
            return UnknownKindProblem();
        }

        if (file.Length <= 0)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "A non-empty file is required.",
                extensions: new Dictionary<string, object?> { ["code"] = "branding.file_required" });
        }

        try
        {
            await using Stream content = file.OpenReadStream();
            FileUpload upload = new(
                content, file.FileName, file.ContentType, file.Length, currentUserAccessor.Handle ?? "unknown");
            return Results.Ok(await brandingService.UploadLogoAsync(assetKind, upload, cancellationToken));
        }
        catch (FileValidationException exception)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: exception.Message,
                extensions: new Dictionary<string, object?> { ["code"] = "branding.invalid_file" });
        }
    }

    private static async Task<IResult> DeleteLogoAsync(string kind, BrandingService brandingService, CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out BrandingAssetKind assetKind))
        {
            return UnknownKindProblem();
        }

        bool deleted = await brandingService.DeleteLogoAsync(assetKind, cancellationToken);
        return deleted ? Results.NoContent() : NotFoundProblem();
    }

    private static async Task<IResult> GetLogoAsync(string kind, BrandingService brandingService, CancellationToken cancellationToken)
    {
        if (!TryParseKind(kind, out BrandingAssetKind assetKind))
        {
            return UnknownKindProblem();
        }

        (Stream Content, string ContentType)? logo = await brandingService.OpenLogoAsync(assetKind, cancellationToken);
        return logo is null ? NotFoundProblem() : Results.Stream(logo.Value.Content, logo.Value.ContentType);
    }

    private static bool TryParseKind(string kind, out BrandingAssetKind assetKind) =>
        Enum.TryParse(kind, ignoreCase: true, out assetKind) && Enum.IsDefined(assetKind);

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Branding asset not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "branding.not_found" });

    private static IResult UnknownKindProblem() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Unknown branding asset kind.",
        extensions: new Dictionary<string, object?> { ["code"] = "branding.unknown_kind" });

    private static IResult InvalidThemeTokenProblem() => Results.Problem(
        statusCode: StatusCodes.Status422UnprocessableEntity,
        title: "One or more theme tokens are not allow-listed or have an invalid value.",
        extensions: new Dictionary<string, object?> { ["code"] = "branding.invalid_theme_token" });
}
