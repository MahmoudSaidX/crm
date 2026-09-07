using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SquadCrm.Infrastructure.Postgres;

namespace SquadCrm.Modules.TicketManagement.Persistence;

public sealed class TicketManagementDbContextFactory : IDesignTimeDbContextFactory<TicketManagementDbContext>
{
    public TicketManagementDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        string connectionString = configuration.ReadPostgresOptions().BuildConnectionString();
        DbContextOptionsBuilder<TicketManagementDbContext> options = new();

        // Mirrors the interceptor wiring in TicketManagementModule.RegisterServices
        // (no ICorrelationIdAccessor outside an HTTP request — the interceptor
        // falls back to a freshly generated id per save, same as
        // ArchitectureFixtureDbContextOptions.Apply). Kept duplicated rather
        // than extracted into a shared `*Options` helper (a single interceptor
        // does not warrant one), but both call sites must add it so `dotnet ef`
        // and the persistence integration suite (which builds its context
        // through this same factory) exercise the exact same outbox path the
        // running application does.
        options.UseNpgsql(connectionString, npgsql =>
            npgsql.MigrationsHistoryTable(
                TicketManagementSchema.MigrationsHistoryTable, TicketManagementSchema.Name))
            .AddInterceptors(new TicketManagementOutboxInterceptor());
        return new TicketManagementDbContext(options.Options);
    }
}
