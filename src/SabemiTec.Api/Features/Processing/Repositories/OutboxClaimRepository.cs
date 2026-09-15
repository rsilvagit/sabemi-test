using Dapper;
using Npgsql;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Persistence.Sql;

namespace SabemiTec.Api.Features.Processing.Repositories;

/// <summary>
/// Claim and failure run in autocommit directly against the NpgsqlDataSource
/// (UPDATE...RETURNING is already atomic on its own — wrapping it in a transaction would
/// just add round-trips). MarkProcessed uses the current IUnitOfWork because it needs to
/// share the SAME transaction as the contract upsert — that's the piece that guarantees the
/// exactly-once effect (see PaymentEventProcessor).
/// </summary>
internal sealed class OutboxClaimRepository(NpgsqlDataSource dataSource, IUnitOfWork uow) : IOutboxClaimRepository
{
    public async Task<IReadOnlyList<ClaimedPaymentEvent>> ClaimBatchAsync(int batchSize, int leaseSeconds, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            PaymentEventSql.ClaimBatch,
            new { BatchSize = batchSize, LeaseSeconds = leaseSeconds },
            cancellationToken: ct);

        var rows = await conn.QueryAsync<ClaimedPaymentEvent>(cmd);
        return rows.AsList();
    }

    public async Task<int> MarkProcessedAsync(long id, CancellationToken ct)
    {
        var connection = await uow.EnsureOpenAsync(ct);
        var cmd = new CommandDefinition(PaymentEventSql.MarkProcessed, new { Id = id }, uow.Transaction, cancellationToken: ct);
        return await connection.ExecuteAsync(cmd);
    }

    public async Task MarkFailedOrDeadLetteredAsync(long id, int maxAttempts, int retryDelaySeconds, string error, CancellationToken ct)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        var cmd = new CommandDefinition(
            PaymentEventSql.MarkFailedOrDeadLettered,
            new { Id = id, MaxAttempts = maxAttempts, RetryDelaySeconds = retryDelaySeconds, Error = error },
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }
}
