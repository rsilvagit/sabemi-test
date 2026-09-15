using Dapper;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Persistence.Sql;

namespace SabemiTec.Api.Features.Webhooks.Repositories;

/// <summary>Never opens its own connection — always uses the current IUnitOfWork's.</summary>
internal sealed class PaymentEventRepository(IUnitOfWork uow) : IPaymentEventRepository
{
    public async Task<long?> InsertIfNotExistsAsync(InsertPaymentEventCommand command, CancellationToken ct)
    {
        var connection = await uow.EnsureOpenAsync(ct);
        var cmd = new CommandDefinition(PaymentEventSql.InsertIfNotExists, command, uow.Transaction, cancellationToken: ct);
        return await connection.ExecuteScalarAsync<long?>(cmd);
    }

    public async Task<ExistingPaymentEvent?> FindByTransactionIdAsync(string transactionId, CancellationToken ct)
    {
        var connection = await uow.EnsureOpenAsync(ct);
        var cmd = new CommandDefinition(PaymentEventSql.FindByTransactionId, new { TransactionId = transactionId }, uow.Transaction, cancellationToken: ct);
        return await connection.QuerySingleOrDefaultAsync<ExistingPaymentEvent>(cmd);
    }
}
