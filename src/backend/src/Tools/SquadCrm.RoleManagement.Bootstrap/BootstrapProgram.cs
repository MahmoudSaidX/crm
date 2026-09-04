using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SquadCrm.Infrastructure.Postgres;
using SquadCrm.Modules.Audit;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;
using SquadCrm.Modules.RoleManagement;
using SquadCrm.Modules.RoleManagement.Persistence;
using SquadCrm.Modules.StaffIdentity;
using SquadCrm.Modules.StaffIdentity.Contracts;
using SquadCrm.Modules.StaffIdentity.Persistence;

namespace SquadCrm.Tools.RoleManagementBootstrap;

public static class BootstrapProgram
{
    public static async Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length is not (4 or 6)
            || !TryReadOption(args, "--subject-email", out string? subjectEmail)
            || !TryReadOption(args, "--role-code", out string? roleCode)
            || string.IsNullOrWhiteSpace(subjectEmail)
            || string.IsNullOrWhiteSpace(roleCode))
        {
            Console.Error.WriteLine(
                "Usage: --subject-email <existing-email> --role-code <role-code> [--role-name <role-name>]");
            return 2;
        }

        TryReadOption(args, "--role-name", out string? roleName);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        string connectionString;
        try
        {
            // Same derivation the API composition root uses (CRM-197): reads and
            // validates POSTGRES_*, then publishes ConnectionStrings:SquadCrmPostgres
            // for GetSquadCrmPostgresConnectionString() to read below.
            builder.AddSquadCrmPostgres();
            connectionString = builder.Configuration.GetSquadCrmPostgresConnectionString();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            Console.Error.WriteLine("PostgreSQL configuration is invalid or incomplete.");
            return 2;
        }

        builder.Services.AddDbContext<StaffIdentityDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddDbContext<RoleManagementDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddDbContext<AuditDbContext>(options => options.UseNpgsql(connectionString));
        builder.Services.AddScoped<IStaffSubjectReferenceReader, StaffSubjectReferenceReader>();
        builder.Services.AddScoped<IAuditRecorder, AuditRecorder>();
        builder.Services.AddScoped<AuthorizationBootstrapService>();

        using IHost host = builder.Build();
        await using AsyncServiceScope scope = host.Services.CreateAsyncScope();
        AuthorizationBootstrapResult result = await scope.ServiceProvider
            .GetRequiredService<AuthorizationBootstrapService>()
            .BootstrapAsync(subjectEmail, roleCode, roleName, cancellationToken);

        if (!result.Succeeded)
        {
            Console.Error.WriteLine(result.Failure switch
            {
                AuthorizationBootstrapFailure.SubjectNotFound => "The target staff subject was not found.",
                AuthorizationBootstrapFailure.SubjectInactive => "The target staff subject is inactive.",
                AuthorizationBootstrapFailure.RoleInactive => "The target role is inactive.",
                AuthorizationBootstrapFailure.RoleConflict =>
                    "The role could not be created due to a concurrent bootstrap run; re-run the command.",
                _ => "Authorization bootstrap failed.",
            });
            return 1;
        }

        Console.WriteLine("Authorization bootstrap completed.");
        return 0;
    }

    private static bool TryReadOption(string[] args, string name, out string? value)
    {
        value = null;
        int index = Array.IndexOf(args, name);
        if (index < 0 || index == args.Length - 1 || args.Count(item => item == name) != 1)
        {
            return false;
        }

        value = args[index + 1];
        return !value.StartsWith("--", StringComparison.Ordinal);
    }
}
