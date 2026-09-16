using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Processing.Repositories;

/// <summary>Always runs inside the current IUnitOfWork transaction — never standalone.</summary>
internal sealed class ContractStatusRepository(IDatabaseConnection db) : IContractStatusRepository
{
    public async Task UpsertAsync(UpsertContractStatusCommand command, CancellationToken ct)
    {
        var cmd = new CommandDefinition(ContractStatusSql.Upsert, command, db.Transaction, cancellationToken: ct);
        await db.Connection.ExecuteAsync(cmd);
    }
}
