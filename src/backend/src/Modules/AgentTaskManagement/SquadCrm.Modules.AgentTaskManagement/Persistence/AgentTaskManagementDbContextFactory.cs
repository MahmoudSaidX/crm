using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.AgentTaskManagement.Persistence;

public sealed class AgentTaskManagementDbContextFactory : IDesignTimeDbContextFactory<AgentTaskManagementDbContext>
{
    public AgentTaskManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<AgentTaskManagementDbContext> options = new();

        // Mirrors TicketManagementDbContextFactory: both `dotnet ef` and the
        // persistence integration suite build the context through this same
        // factory, so both exercise the exact outbox path the running
        // application does.
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                AgentTaskManagementSchema.MigrationsHistoryTable, AgentTaskManagementSchema.Name))
            .AddInterceptors(new AgentTaskManagementOutboxInterceptor());
        return new AgentTaskManagementDbContext(options.Options);
    }
}
