using Dapper;
using Npgsql;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Processing.Repositories;

/// <summary>
/// Claim and failure run in autocommit on their own ad-hoc connection (UPDATE...RETURNING is
/// already atomic on its own — wrapping it in a transaction would just add round-trips), and
/// deliberately never share the current IUnitOfWork's connection: if the caller's transaction
/// just failed because of a connection-level error, that connection may be unusable.
/// MarkProcessed uses the current IUnitOfWork's connection because it needs to share the SAME
/// transaction as the contract upsert — that's the piece that guarantees the exactly-once
/// effect (see PaymentEventProcessor).
/// </summary>
internal sealed class OutboxClaimRepository(IConfiguration configuration, IDatabaseConnection db) : IOutboxClaimRepository
{
    private string ConnectionString => configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

    public async Task<IReadOnlyList<ClaimedPaymentEvent>> ClaimBatchAsync(int batchSize, int leaseSeconds, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            PaymentEventSql.ClaimBatch,
            new { BatchSize = batchSize, LeaseSeconds = leaseSeconds },
            cancellationToken: ct);

        var rows = await conn.QueryAsync<ClaimedPaymentEvent>(cmd);
        return rows.AsList();
    }

    public async Task<int> MarkProcessedAsync(long id, CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentEventSql.MarkProcessed, new { Id = id }, db.Transaction, cancellationToken: ct);
        return await db.Connection.ExecuteAsync(cmd);
    }

    public async Task MarkFailedOrDeadLetteredAsync(long id, int maxAttempts, int retryDelaySeconds, string error, CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync(ct);

        var cmd = new CommandDefinition(
            PaymentEventSql.MarkFailedOrDeadLettered,
            new { Id = id, MaxAttempts = maxAttempts, RetryDelaySeconds = retryDelaySeconds, Error = error },
            cancellationToken: ct);

        await conn.ExecuteAsync(cmd);
    }
}
