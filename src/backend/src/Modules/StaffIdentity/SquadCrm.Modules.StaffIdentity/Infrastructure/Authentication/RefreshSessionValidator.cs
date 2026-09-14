using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.Modules.StaffIdentity.Infrastructure.Persistence;

namespace SquadCrm.Modules.StaffIdentity.Infrastructure.Authentication;

/// <summary>
/// Rejects a bearer token whose refresh session has been revoked or has
/// expired. Wired as the JWT <c>OnTokenValidated</c> event by
/// <see cref="StaffIdentityModule"/>; it reads module-owned persistence and is
/// therefore infrastructure, not an HTTP endpoint.
/// </summary>
internal static class RefreshSessionValidator
{
    public static async Task ValidateActiveSessionAsync(TokenValidatedContext context)
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
}
