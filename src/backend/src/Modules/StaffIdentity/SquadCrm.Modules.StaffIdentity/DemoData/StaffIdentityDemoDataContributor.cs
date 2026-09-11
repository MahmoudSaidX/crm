using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SquadCrm.BuildingBlocks.Abstractions.DemoData;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Modules.StaffIdentity.DemoData;

/// <summary>
/// StaffIdentity's own demo-data contributor: the five Development demo
/// personas. Runs only when the demo seeder tool invokes it — nothing wires it
/// into the application host, so it can never execute at startup.
/// <para>
/// Deliberately separate from <c>Bootstrap.BootstrapProgram</c>, which stays the
/// operator tool for a single real account and is not modified: that tool resets
/// the password of an existing account and revokes its sessions, which is the
/// wrong behavior for an idempotent demo seed run.
/// </para>
/// </summary>
public sealed class StaffIdentityDemoDataContributor : IDemoDataContributor
{
    /// <summary>
    /// Demo-only password, used when <see cref="PasswordEnvironmentVariable"/> is
    /// unset. Local Development/Test seeding only — the production bootstrap path
    /// never reads it, and it is never written to a log or to the run summary.
    /// </summary>
    internal const string DefaultDemoPassword = "SquadDemo!2026";

    internal const string PasswordEnvironmentVariable = "SQUADCRM_DEMO_PASSWORD";

    private const string SeederHandle = "demo-data-seeder";

    /// <summary>
    /// The demo personas, in a fixed order so the run is deterministic. The role
    /// codes are consumed by RoleManagement's contributor, which owns roles.
    /// </summary>
    internal static readonly (string Email, string DisplayName, string RoleCode, string Department, string Branch)[]
        Personas =
        [
            ("admin@squadcrm.local", "Ahmed Admin", "administrator", "Operations", "Riyadh — Head Office"),
            ("manager@squadcrm.local", "Sara Manager", "support-manager", "Customer Support", "Riyadh — Head Office"),
            ("agent1@squadcrm.local", "Omar Hassan", "support-agent", "Customer Support", "Riyadh — Olaya"),
            ("agent2@squadcrm.local", "Nour Khaled", "support-agent", "Technical Support", "Jeddah"),
            ("viewer@squadcrm.local", "Youssef Viewer", "read-only", "Customer Success", "Dammam"),
        ];

    public string Name => "StaffIdentity";

    public async Task<DemoDataOutcome> SeedAsync(DemoDataScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        string password = Environment.GetEnvironmentVariable(PasswordEnvironmentVariable) ?? DefaultDemoPassword;
        ValidatePassword(password);

        int created = 0;
        int reconciled = 0;

        try
        {
            await using StaffIdentityDbContext dbContext = new StaffIdentityDbContextFactory().CreateDbContext([]);
            IPasswordHasher<StaffUser> passwordHasher = new PasswordHasher<StaffUser>();

            foreach ((string email, string displayName, string roleCode, string department, string branch)
                in Personas)
            {
                string normalizedEmail = AuthenticationService.NormalizeEmail(email);
                StaffUser? user = await dbContext.StaffUsers.SingleOrDefaultAsync(
                    candidate => candidate.NormalizedEmail == normalizedEmail, cancellationToken);

                if (user is null)
                {
                    user = new StaffUser
                    {
                        Id = Guid.NewGuid(),
                        NormalizedEmail = normalizedEmail,
                        PasswordHash = string.Empty,
                        DisplayName = displayName,
                        Department = department,
                        Branch = branch,
                        IsActive = true,
                        CreatedAtUtc = scope.NowUtc,
                    };

                    // The existing StaffIdentity hasher, never a hand-written hash.
                    user.PasswordHash = passwordHasher.HashPassword(user, password);
                    dbContext.StaffUsers.Add(user);
                    dbContext.AuthenticationEvents.Add(new AuthenticationEvent
                    {
                        StaffUserId = user.Id,
                        EventType = "user_created",
                        Outcome = "succeeded",
                        ChangedByHandle = SeederHandle,
                        OccurredAtUtc = scope.NowUtc,
                    });
                    created++;
                }
                else
                {
                    // An existing account's password hash is left alone: a developer
                    // may have changed it deliberately, and rewriting it on every
                    // seed run would be a surprise, not idempotency.
                    bool changed = user.DisplayName != displayName
                        || user.Department != department
                        || user.Branch != branch
                        || !user.IsActive;
                    user.DisplayName = displayName;
                    user.Department = department;
                    user.Branch = branch;
                    user.IsActive = true;
                    if (changed)
                    {
                        reconciled++;
                    }
                }

                scope.References.Staff.Add(new DemoStaffReference(email, user.Id, displayName, roleCode));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            password = string.Empty;
        }

        return new DemoDataOutcome(
            Name,
            new Dictionary<string, int>
            {
                ["staff users created"] = created,
                ["staff users reconciled"] = reconciled,
                ["staff users total"] = Personas.Length,
            });
    }

    private static void ValidatePassword(string password)
    {
        // Mirrors the bootstrap tool's bounds so a demo password cannot be
        // weaker than a bootstrapped one. The value itself is never echoed.
        if (password.Length is < 8 or > 256)
        {
            throw new InvalidOperationException(
                $"The demo password must contain between 8 and 256 characters. "
                + $"Set {PasswordEnvironmentVariable} to a valid value.");
        }
    }
}
