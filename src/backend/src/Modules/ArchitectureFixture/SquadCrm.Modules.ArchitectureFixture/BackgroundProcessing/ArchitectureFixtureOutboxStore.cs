using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SquadCrm.Modules.ArchitectureFixture.Persistence;

namespace SquadCrm.Modules.ArchitectureFixture.BackgroundProcessing;

public sealed class ArchitectureFixtureOutboxStore(ArchitectureFixtureDbContext dbContext)
{
    internal async Task<IReadOnlyList<ClaimedOutboxMessage>> ClaimAsync(
        Guid leaseId,
        DateTimeOffset nowUtc,
        OutboxProcessingOptions options,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        NpgsqlConnection connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        await using NpgsqlCommand command = connection.CreateCommand();
        command.Transaction = (NpgsqlTransaction)transaction.GetDbTransaction();
        command.CommandText = """
            UPDATE architecture_fixture.outbox_message AS message
            SET lease_id = @lease_id,
                leased_until_utc = @leased_until_utc
            WHERE message.id IN (
                SELECT candidate.id
                FROM architecture_fixture.outbox_message AS candidate
                WHERE candidate.processed_at_utc IS NULL
                  AND candidate.retry_count < @retry_ceiling
                  AND (candidate.next_attempt_at_utc IS NULL OR candidate.next_attempt_at_utc <= @now_utc)
                  AND (candidate.lease_id IS NULL OR candidate.leased_until_utc <= @now_utc)
                ORDER BY candidate.occurred_at_utc, candidate.id
                FOR UPDATE SKIP LOCKED
                LIMIT @batch_size
            )
            RETURNING message.id, message.type, message.payload, message.correlation_id;
            """;
        command.Parameters.AddWithValue("lease_id", leaseId);
        command.Parameters.AddWithValue("leased_until_utc", nowUtc.AddSeconds(options.LeaseSeconds));
        command.Parameters.AddWithValue("retry_ceiling", options.RetryCeiling);
        command.Parameters.AddWithValue("now_utc", nowUtc);
        command.Parameters.AddWithValue("batch_size", options.BatchSize);

        List<ClaimedOutboxMessage> claimed = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            claimed.Add(new ClaimedOutboxMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        await reader.CloseAsync();
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    internal async Task MarkProcessedAsync(Guid messageId, Guid leaseId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        OutboxMessage message = await OwnedMessage(messageId, leaseId, cancellationToken);
        message.MarkProcessed(leaseId, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    internal async Task MarkFailedAsync(
        Guid messageId,
        Guid leaseId,
        DateTimeOffset nowUtc,
        OutboxProcessingOptions options,
        string error,
        CancellationToken cancellationToken)
    {
        OutboxMessage message = await OwnedMessage(messageId, leaseId, cancellationToken);
        int nextRetryCount = message.RetryCount + 1;
        DateTimeOffset? nextAttempt = nextRetryCount < options.RetryCeiling
            ? nowUtc.AddSeconds(options.BackoffSeconds * nextRetryCount)
            : null;
        message.MarkFailed(leaseId, error, nextAttempt);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<OutboxMessage> OwnedMessage(Guid messageId, Guid leaseId, CancellationToken cancellationToken) =>
        dbContext.OutboxMessages.SingleAsync(
            message => message.Id == messageId && message.LeaseId == leaseId,
            cancellationToken);
}
