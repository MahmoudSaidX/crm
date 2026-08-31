using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SquadCrm.BuildingBlocks.Http;
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.BuildingBlocks.Validation;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.StaffIdentity.Persistence;
using SquadCrm.Modules.StaffIdentity.Contracts;

namespace SquadCrm.Modules.StaffIdentity;

public sealed class StaffIdentityModule : IModule
{
    private const string RefreshCookieName = "squadcrm_refresh";

    public string Name => "StaffIdentity";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthenticationOptions>()
            .Bind(configuration.GetSection(AuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        AuthenticationOptions authentication = configuration
            .GetSection(AuthenticationOptions.SectionName)
            .Get<AuthenticationOptions>() ?? new AuthenticationOptions();
        Validator.ValidateObject(authentication, new ValidationContext(authentication), validateAllProperties: true);

        services.AddDbContext<StaffIdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetSquadCrmPostgresConnectionString(), npgsql =>
                npgsql.MigrationsHistoryTable(
                    StaffIdentitySchema.MigrationsHistoryTable,
                    StaffIdentitySchema.Name)));
        services.AddScoped<AuthenticationService>();
        services.AddScoped<StaffUserService>();
        services.AddScoped<IPasswordHasher<StaffUser>, PasswordHasher<StaffUser>>();
        services.AddScoped<ICurrentUserAccessor, HttpCurrentUserAccessor>();
        services.AddScoped<IStaffSubjectReferenceReader, StaffSubjectReferenceReader>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authentication.Issuer,
                    ValidateAudience = true,
                    ValidAudience = authentication.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authentication.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = ValidateActiveSessionAsync,
                };
            });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth-login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.AddPolicy("auth-refresh", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
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
        staffUsers.MapGet("", ListStaffUsersAsync).RequireAuthorization(UsersViewPolicy);
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
        string? search,
        StaffUserService staffUserService,
        CancellationToken cancellationToken)
    {
        PagedResult<Persistence.StaffUser> page = await staffUserService.ListAsync(pagination, search, cancellationToken);
        return Results.Ok(new PagedResult<StaffUserResponse>(
            page.Items.Select(ToResponse).ToList(), page.Page, page.PageSize, page.TotalCount));
    }

    private static async Task<IResult> GetStaffUserAsync(
        Guid id, StaffUserService staffUserService, CancellationToken cancellationToken)
    {
        Persistence.StaffUser? user = await staffUserService.GetAsync(id, cancellationToken);
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

    private static StaffUserResponse ToResponse(Persistence.StaffUser user) => new(
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

    private static async Task ValidateActiveSessionAsync(TokenValidatedContext context)
    {
        string? userClaim = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        string? sessionClaim = context.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sid);
        if (!Guid.TryParse(userClaim, out Guid userId) || !Guid.TryParse(sessionClaim, out Guid sessionId))
        {
            context.Fail("Invalid session claims.");
            return;
        }

        StaffIdentityDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<StaffIdentityDbContext>();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool active = await dbContext.RefreshSessions.AnyAsync(
            session => session.Id == sessionId
                && session.StaffUserId == userId
                && session.RevokedAtUtc == null
                && session.ExpiresAtUtc > now
                && session.StaffUser.IsActive,
            context.HttpContext.RequestAborted);
        if (!active)
        {
            context.Fail("Session is not active.");
        }
    }

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
