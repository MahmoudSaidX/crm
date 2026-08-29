using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Modules.StaffIdentity;

internal sealed class AuthenticationService(
    StaffIdentityDbContext dbContext,
    IPasswordHasher<StaffUser> passwordHasher,
    IOptions<AuthenticationOptions> options)
{
    private static readonly StaffUser DummyUser = new()
    {
        Id = Guid.Empty,
        NormalizedEmail = "DUMMY@INVALID",
        PasswordHash = string.Empty,
        IsActive = false,
        CreatedAtUtc = DateTimeOffset.UnixEpoch,
    };
    private static readonly string DummyPasswordHash =
        new PasswordHasher<StaffUser>().HashPassword(DummyUser, "not-a-real-password");
    private readonly AuthenticationOptions _options = options.Value;

    public async Task<AuthenticationResult?> SignInAsync(
        string email,
        string password,
        bool rememberSession,
        CancellationToken cancellationToken)
    {
        string normalizedEmail = NormalizeEmail(email);
        StaffUser? user = await dbContext.StaffUsers
            .SingleOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

        StaffUser passwordSubject = user ?? DummyUser;
        PasswordVerificationResult verification = passwordHasher.VerifyHashedPassword(
            passwordSubject,
            user?.PasswordHash ?? DummyPasswordHash,
            password);

        if (user is null || !user.IsActive || verification == PasswordVerificationResult.Failed)
        {
            await RecordEventAsync(user?.Id, "sign_in", "rejected", cancellationToken);
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        AuthenticationResult result = CreateSession(user, rememberSession);
        await RecordEventAsync(user.Id, "sign_in", "succeeded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task<AuthenticationResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        string tokenHash = HashToken(refreshToken);
        RefreshSession? session = await dbContext.RefreshSessions
            .Include(candidate => candidate.StaffUser)
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);

        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= now || !session.StaffUser.IsActive)
        {
            await RecordEventAsync(session?.StaffUserId, "refresh", "rejected", cancellationToken);
            return null;
        }

        int revoked = await dbContext.RefreshSessions
            .Where(candidate => candidate.Id == session.Id && candidate.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(candidate => candidate.RevokedAtUtc, now),
                cancellationToken);
        if (revoked != 1)
        {
            await RecordEventAsync(session.StaffUserId, "refresh", "rejected", cancellationToken);
            return null;
        }

        AuthenticationResult result = CreateSession(
            session.StaffUser,
            session.ExpiresAtUtc - session.CreatedAtUtc > TimeSpan.FromDays(_options.RefreshSessionDays));
        session.RevokedAtUtc = now;
        session.ReplacedBySessionId = result.SessionId;
        await RecordEventAsync(session.StaffUserId, "refresh", "succeeded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    public async Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        RefreshSession? session = null;
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            string tokenHash = HashToken(refreshToken);
            session = await dbContext.RefreshSessions
                .SingleOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken);
        }

        if (session is not null && session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTimeOffset.UtcNow;
        }

        await RecordEventAsync(session?.StaffUserId, "sign_out", "succeeded", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private AuthenticationResult CreateSession(StaffUser user, bool rememberSession)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset accessExpiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        DateTimeOffset refreshExpiresAt = now.AddDays(
            rememberSession ? _options.RememberedSessionDays : _options.RefreshSessionDays);
        string refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        RefreshSession session = new()
        {
            Id = Guid.NewGuid(),
            StaffUserId = user.Id,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiresAt,
        };
        dbContext.RefreshSessions.Add(session);

        Claim[] claims =
        [
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Sid, session.Id.ToString()),
        ];
        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken token = new(
            _options.Issuer,
            _options.Audience,
            claims,
            now.UtcDateTime,
            accessExpiresAt.UtcDateTime,
            credentials);

        return new AuthenticationResult(
            new JwtSecurityTokenHandler().WriteToken(token),
            accessExpiresAt,
            refreshToken,
            refreshExpiresAt,
            session.Id);
    }

    private async Task RecordEventAsync(
        Guid? staffUserId,
        string eventType,
        string outcome,
        CancellationToken cancellationToken)
    {
        dbContext.AuthenticationEvents.Add(new AuthenticationEvent
        {
            StaffUserId = staffUserId,
            EventType = eventType,
            Outcome = outcome,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed record AuthenticationResult(
    string AccessToken,
    DateTimeOffset AccessExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshExpiresAt,
    Guid SessionId);
