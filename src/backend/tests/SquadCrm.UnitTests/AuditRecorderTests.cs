using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SquadCrm.Modules.Audit;
using SquadCrm.Modules.Audit.Contracts;
using SquadCrm.Modules.Audit.Persistence;

namespace SquadCrm.UnitTests;

/// <summary>
/// Proves the best-effort transaction-boundary contract from Story CRM-114:
/// a failing <c>SaveChangesAsync</c> must be swallowed and logged, never
/// rethrown, so a caller's own business write is never rolled back by an
/// audit-write failure.
/// </summary>
public sealed class AuditRecorderTests
{
    [Fact]
    public async Task RecordAsync_SwallowsSaveChangesFailure_AndLogsIt_WithoutRethrowing()
    {
        DbContextOptions<AuditDbContext> options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        AuditDbContext context = new(options);
        FakeLogger logger = new();
        AuditRecorder recorder = new(context, logger);

        // Disposing the context first forces SaveChangesAsync to throw, simulating
        // a failure of the separate AuditDbContext connection without needing a
        // real, briefly-unreachable PostgreSQL server.
        await context.DisposeAsync();

        AuditRecordRequest request = new("bootstrap-tool", "role_assigned", "StaffSubjectRole", "id:id");

        Exception? observed = await Record.ExceptionAsync(
            () => recorder.RecordAsync(request, CancellationToken.None));

        Assert.Null(observed);
        Assert.True(logger.ErrorLogged, "AuditRecorder.RecordAsync must log the failure it swallows.");
    }

    [Fact]
    public async Task RecordAsync_Succeeds_PersistsOneRecord()
    {
        DbContextOptions<AuditDbContext> options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using AuditDbContext context = new(options);
        AuditRecorder recorder = new(context, new FakeLogger());

        await recorder.RecordAsync(
            new AuditRecordRequest("bootstrap-tool", "role_assigned", "StaffSubjectRole", "subject:role"),
            CancellationToken.None);

        Assert.Equal(1, await context.AuditRecords.CountAsync());
    }

    private sealed class FakeLogger : ILogger<AuditRecorder>
    {
        public bool ErrorLogged { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                ErrorLogged = true;
            }
        }
    }
}
