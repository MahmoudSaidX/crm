using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Modules.StaffIdentity.Bootstrap;

public static class BootstrapProgram
{
    internal const string PasswordEnvironmentVariable = "SQUADCRM_BOOTSTRAP_STAFF_PASSWORD";

    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        try
        {
            EnsureDevelopment(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
            if (args.Length != 1)
            {
                throw new InvalidOperationException(
                    "Usage: dotnet run --project src/Modules/StaffIdentity/" +
                    "SquadCrm.Modules.StaffIdentity.Bootstrap -- <staff-email>");
            }

            string password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable)
                ?? ReadConfirmedPassword();
            try
            {
                await using StaffIdentityDbContext dbContext =
                    new StaffIdentityDbContextFactory().CreateDbContext([]);
                await CreateOrResetAsync(
                    "Development",
                    args[0],
                    password,
                    dbContext,
                    new PasswordHasher<StaffUser>(),
                    cancellationToken);
            }
            finally
            {
                password = string.Empty;
            }

            Console.WriteLine("Local staff account is ready.");
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            Console.Error.WriteLine(exception.Message);
            return 2;
        }
    }

    internal static async Task CreateOrResetAsync(
        string? environment,
        string email,
        string password,
        StaffIdentityDbContext dbContext,
        IPasswordHasher<StaffUser> passwordHasher,
        CancellationToken cancellationToken)
    {
        EnsureDevelopment(environment);
        ValidateInputs(email, password);

        string normalizedEmail = email.Trim().ToUpperInvariant();
        StaffUser? user = await dbContext.StaffUsers.SingleOrDefaultAsync(
            candidate => candidate.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null)
        {
            user = new StaffUser
            {
                Id = Guid.NewGuid(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = string.Empty,
                IsActive = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
            };
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            dbContext.StaffUsers.Add(user);
        }
        else
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            user.IsActive = true;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            await dbContext.RefreshSessions
                .Where(session => session.StaffUserId == user.Id && session.RevokedAtUtc == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(session => session.RevokedAtUtc, now),
                    cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal static void EnsureDevelopment(string? environment)
    {
        if (!string.Equals(environment, "Development", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Staff bootstrap is allowed only when ASPNETCORE_ENVIRONMENT=Development.");
        }
    }

    private static void ValidateInputs(string email, string password)
    {
        string trimmedEmail = email.Trim();
        if (trimmedEmail.Length > 320 || !new EmailAddressAttribute().IsValid(trimmedEmail))
        {
            throw new InvalidOperationException("Enter a valid staff email address.");
        }

        if (password.Length is < 8 or > 256)
        {
            throw new InvalidOperationException("The password must contain between 8 and 256 characters.");
        }
    }

    private static string ReadConfirmedPassword()
    {
        if (Console.IsInputRedirected)
        {
            throw new InvalidOperationException(
                $"Interactive password input is unavailable. Set {PasswordEnvironmentVariable} " +
                "for this process only.");
        }

        string password = ReadPassword("Password: ");
        string confirmation = ReadPassword("Confirm password: ");
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Passwords do not match.");
        }

        return password;
    }

    private static string ReadPassword(string prompt)
    {
        Console.Error.Write(prompt);
        StringBuilder password = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}
