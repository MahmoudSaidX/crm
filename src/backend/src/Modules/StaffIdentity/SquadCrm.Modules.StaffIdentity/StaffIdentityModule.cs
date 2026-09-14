using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
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
using SquadCrm.BuildingBlocks.Modules;
using SquadCrm.BuildingBlocks.Security;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.StaffIdentity.Application.Services;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.StaffIdentity.Domain.Entities;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Authentication;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Persistence;

using SquadCrm.Modules.StaffIdentity.Presentation.Endpoints;

namespace SquadCrm.Modules.StaffIdentity;

public sealed class StaffIdentityModule : IModule
{

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
                    OnTokenValidated = RefreshSessionValidator.ValidateActiveSessionAsync,
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

    public void MapEndpoints(IEndpointRouteBuilder endpoints) =>
        StaffIdentityEndpoints.Map(endpoints);
}
