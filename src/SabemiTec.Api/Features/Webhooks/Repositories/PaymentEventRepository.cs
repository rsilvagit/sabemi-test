using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Webhooks.Repositories;

/// <summary>Never opens its own connection — always uses the current request's shared
/// IDatabaseConnection, opened by the caller via IUnitOfWork.Open().</summary>
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
