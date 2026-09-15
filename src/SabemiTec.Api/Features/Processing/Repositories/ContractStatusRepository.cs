using Dapper;
using SabemiTec.Api.Persistence;
using SabemiTec.Api.Persistence.Sql;

namespace SabemiTec.Api.Features.Processing.Repositories;

/// <summary>Always runs inside the current IUnitOfWork transaction — never standalone.</summary>
internal sealed class ContractStatusRepository(IUnitOfWork uow) : IContractStatusRepository
{
    public async Task UpsertAsync(UpsertContractStatusCommand command, CancellationToken ct)
    {
        var connection = await uow.EnsureOpenAsync(ct);
        var cmd = new CommandDefinition(ContractStatusSql.Upsert, command, uow.Transaction, cancellationToken: ct);
        await connection.ExecuteAsync(cmd);
    }
}
