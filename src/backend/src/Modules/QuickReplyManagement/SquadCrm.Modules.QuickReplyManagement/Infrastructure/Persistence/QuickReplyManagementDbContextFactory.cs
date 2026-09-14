using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.QuickReplyManagement.Infrastructure.Persistence;

public sealed class QuickReplyManagementDbContextFactory : IDesignTimeDbContextFactory<QuickReplyManagementDbContext>
{
    public QuickReplyManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<QuickReplyManagementDbContext> options = new();

        // Mirrors DepartmentManagementDbContextFactory: both `dotnet ef` and
        // the persistence integration suite build the context through this
        // same factory.
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                QuickReplyManagementSchema.MigrationsHistoryTable, QuickReplyManagementSchema.Name));
        return new QuickReplyManagementDbContext(options.Options);
    }
}
