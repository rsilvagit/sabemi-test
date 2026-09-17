using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Webhooks.Repositories;

/// <summary>No transaction here on purpose — a single INSERT doesn't need one. Dapper opens
/// and closes the connection per call on its own.</summary>
internal sealed class PaymentEventRepository(IDatabaseConnection db) : IPaymentEventRepository
{
    public async Task<long?> InsertIfNotExistsAsync(InsertPaymentEventCommand command, CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentEventSql.InsertIfNotExists, command, db.Transaction, cancellationToken: ct);
        return await db.Connection.ExecuteScalarAsync<long?>(cmd);
    }

    public async Task<ExistingPaymentEvent?> FindByTransactionIdAsync(string transactionId, CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentEventSql.FindByTransactionId, new { TransactionId = transactionId }, db.Transaction, cancellationToken: ct);
        return await db.Connection.QuerySingleOrDefaultAsync<ExistingPaymentEvent>(cmd);
    }
}
