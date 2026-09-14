using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Modules.StaffIdentity.Application.Services;
using SquadCrm.Modules.StaffIdentity.Domain.Entities;
using SquadCrm.Modules.StaffIdentity.Presentation.Requests;
using SquadCrm.Modules.StaffIdentity.Presentation.Responses;

namespace SquadCrm.Modules.StaffIdentity.Presentation.Endpoints;

/// <summary>
/// HTTP surface of the StaffIdentity module: route definitions, model binding,
/// status mapping and response projection. Composed by
/// <see cref="StaffIdentityModule"/>, which owns registration only.
/// </summary>
internal static class StaffIdentityEndpoints
{
    private const string RefreshCookieName = "squadcrm_refresh";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder auth = endpoints.MapGroup("/api/v1/auth").WithTags("Authentication");

        auth.MapPost("/login", SignInAsync)
            .ValidatesDataAnnotations<SignInRequest>()
            .RequireRateLimiting("auth-login");
        auth.MapPost("/refresh", RefreshAsync).RequireRateLimiting("auth-refresh");
        auth.MapPost("/logout", SignOutAsync).RequireRateLimiting("auth-refresh");
        auth.MapGet("/me", CurrentStaff).RequireAuthorization();

        RouteGroupBuilder staffUsers = endpoints.MapGroup("/api/v1/staff-users").WithTags("StaffUsers");
        staffUsers.MapPost("", CreateStaffUserAsync)
            .ValidatesDataAnnotations<CreateStaffUserRequest>()
            .RequireAuthorization(UsersManagePolicy);
        staffUsers.MapGet("", ListStaffUsersAsync)
            .ValidatesDataAnnotations<PaginationRequest>()
            .ValidatesDataAnnotations<StaffUserListQuery>()
            .RequireAuthorization(UsersViewPolicy);
        staffUsers.MapGet("/{id:guid}", GetStaffUserAsync).RequireAuthorization(UsersViewPolicy);
        staffUsers.MapPut("/{id:guid}", UpdateStaffUserAsync)
            .ValidatesDataAnnotations<UpdateStaffUserRequest>()
            .RequireAuthorization(UsersManagePolicy);
        staffUsers.MapPost("/{id:guid}/activate", ActivateStaffUserAsync).RequireAuthorization(UsersManagePolicy);
        staffUsers.MapPost("/{id:guid}/deactivate", DeactivateStaffUserAsync).RequireAuthorization(UsersManagePolicy);
    }

    // Policy names registered by RoleManagementModule ("permission:<code>" convention from
    // CRM-113); referenced here by string only — no project reference needed, ASP.NET Core
    // resolves authorization policies by name from the shared AuthorizationOptions.
    private const string UsersViewPolicy = "permission:users.view";

    private const string UsersManagePolicy = "permission:users.manage";

    private static async Task<IResult> CreateStaffUserAsync(
        CreateStaffUserRequest request,
        StaffUserService staffUserService,
        CancellationToken cancellationToken)
    {
        StaffUserMutationResult result = await staffUserService.CreateAsync(request, cancellationToken);
        return result.Failure switch
        {
            StaffUserMutationFailure.None => Results.Created(
                $"/api/v1/staff-users/{result.User!.Id}", ToResponse(result.User)),
            StaffUserMutationFailure.DuplicateEmail => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "A staff user with this email already exists.",
                extensions: new Dictionary<string, object?> { ["code"] = "staff_users.duplicate_email" }),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ListStaffUsersAsync(
        [AsParameters] PaginationRequest pagination,
        [AsParameters] StaffUserListQuery query,
        StaffUserService staffUserService,
        CancellationToken cancellationToken)
    {
        PagedResult<StaffUser> page = await staffUserService.ListAsync(pagination, query.Search, cancellationToken);
        return Results.Ok(new PagedResult<StaffUserResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetStaffUserAsync(
        Guid id, StaffUserService staffUserService, CancellationToken cancellationToken)
    {
        StaffUser? user = await staffUserService.GetAsync(id, cancellationToken);
        return user is null ? StaffUserNotFoundProblem() : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> UpdateStaffUserAsync(
        Guid id,
        UpdateStaffUserRequest request,
        StaffUserService staffUserService,
        CancellationToken cancellationToken)
    {
        StaffUserMutationResult result = await staffUserService.UpdateAsync(id, request, cancellationToken);
        return result.Failure switch
        {
            StaffUserMutationFailure.None => Results.Ok(ToResponse(result.User!)),
            StaffUserMutationFailure.NotFound => StaffUserNotFoundProblem(),
            _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> ActivateStaffUserAsync(
        Guid id, StaffUserService staffUserService, CancellationToken cancellationToken)
    {
        StaffUserMutationResult result = await staffUserService.ActivateAsync(id, cancellationToken);
        return result.Failure == StaffUserMutationFailure.NotFound
            ? StaffUserNotFoundProblem()
            : Results.Ok(ToResponse(result.User!));
    }

    private static async Task<IResult> DeactivateStaffUserAsync(
        Guid id, StaffUserService staffUserService, CancellationToken cancellationToken)
    {
        StaffUserMutationResult result = await staffUserService.DeactivateAsync(id, cancellationToken);
        return result.Failure == StaffUserMutationFailure.NotFound
            ? StaffUserNotFoundProblem()
            : Results.Ok(ToResponse(result.User!));
    }

    private static IResult StaffUserNotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Staff user not found.",
        extensions: new Dictionary<string, object?> { ["code"] = "staff_users.not_found" });

    private static StaffUserResponse ToResponse(StaffUser user) => new(
        user.Id, user.NormalizedEmail, user.DisplayName, user.Department, user.Branch,
        user.IsActive, user.CreatedAtUtc);

    private static async Task<IResult> SignInAsync(
        SignInRequest request,
        HttpContext context,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        AuthenticationResult? result = await authenticationService.SignInAsync(
            request.Email,
            request.Password,
            request.RememberSession,
            cancellationToken);
        if (result is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials.", extensions: new Dictionary<string, object?> { ["code"] = "authentication.invalid_credentials" });
        }

        WriteRefreshCookie(context, result);
        return Results.Ok(new AccessCredentialResponse(result.AccessToken, result.AccessExpiresAt));
    }

    private static async Task<IResult> RefreshAsync(
        HttpContext context,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(RefreshCookieName, out string? refreshToken);
        AuthenticationResult? result = string.IsNullOrWhiteSpace(refreshToken)
            ? null
            : await authenticationService.RefreshAsync(refreshToken, cancellationToken);
        if (result is null)
        {
            DeleteRefreshCookie(context);
            return Results.Unauthorized();
        }

        WriteRefreshCookie(context, result);
        return Results.Ok(new AccessCredentialResponse(result.AccessToken, result.AccessExpiresAt));
    }

    private static async Task<IResult> SignOutAsync(
        HttpContext context,
        AuthenticationService authenticationService,
        CancellationToken cancellationToken)
    {
        context.Request.Cookies.TryGetValue(RefreshCookieName, out string? refreshToken);
        await authenticationService.RevokeAsync(refreshToken, cancellationToken);
        DeleteRefreshCookie(context);
        return Results.NoContent();
    }

    private static IResult CurrentStaff(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out Guid userId)
            ? Results.Ok(new CurrentStaffResponse(userId))
            : Results.Unauthorized();

    private static void WriteRefreshCookie(HttpContext context, AuthenticationResult result) =>
        context.Response.Cookies.Append(RefreshCookieName, result.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            Expires = result.RefreshExpiresAt,
            IsEssential = true,
        });

    private static void DeleteRefreshCookie(HttpContext context) =>
        context.Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
        });
}
