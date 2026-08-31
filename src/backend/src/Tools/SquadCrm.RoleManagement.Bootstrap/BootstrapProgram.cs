using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SquadCrm.Infrastructure.Postgres;
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
        if (args.Length != 4
            || !TryReadOption(args, "--subject-email", out string? subjectEmail)
            || !TryReadOption(args, "--role-code", out string? roleCode)
            || string.IsNullOrWhiteSpace(subjectEmail)
            || string.IsNullOrWhiteSpace(roleCode))
        {
            Console.Error.WriteLine("Usage: --subject-email <existing-email> --role-code <existing-active-role-code>");
            return 2;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString;
        try
        {
            connectionString = configuration.GetSquadCrmPostgresConnectionString();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            Console.Error.WriteLine("PostgreSQL configuration is invalid or incomplete.");
            return 2;
        }

        ServiceCollection services = new();
        services.AddDbContext<StaffIdentityDbContext>(options => options.UseNpgsql(connectionString));
        services.AddDbContext<RoleManagementDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IStaffSubjectReferenceReader, StaffSubjectReferenceReader>();
        services.AddScoped<AuthorizationBootstrapService>();

        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        AuthorizationBootstrapResult result = await scope.ServiceProvider
            .GetRequiredService<AuthorizationBootstrapService>()
            .BootstrapAsync(subjectEmail, roleCode, cancellationToken);

        if (!result.Succeeded)
        {
            Console.Error.WriteLine(result.Failure switch
            {
                AuthorizationBootstrapFailure.SubjectNotFound => "The target staff subject was not found.",
                AuthorizationBootstrapFailure.SubjectInactive => "The target staff subject is inactive.",
                AuthorizationBootstrapFailure.RoleNotFound => "The target role was not found.",
                AuthorizationBootstrapFailure.RoleInactive => "The target role is inactive.",
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
