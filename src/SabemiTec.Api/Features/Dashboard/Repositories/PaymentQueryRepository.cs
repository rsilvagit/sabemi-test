using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Dashboard.Repositories;

/// <summary>Read-only — no transaction here on purpose, just the shared request connection
/// opened by the route handler via IUnitOfWork.Open().</summary>
internal sealed class PaymentQueryRepository(IDatabaseConnection db) : IPaymentQueryRepository
{
    public async Task<(IReadOnlyList<PaymentListItem> Items, bool HasMore)> SearchAsync(PaymentQuery query, CancellationToken ct)
    {
        var predicates = new List<string>();
        var parameters = new DynamicParameters();

        // Fragments are code-level constants; every value goes through DynamicParameters —
        // never string interpolation of user input.
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            predicates.Add("effective_status = @Status");
            parameters.Add("Status", query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.ContractId))
        {
            predicates.Add("contract_id ilike @ContractPrefix");
            parameters.Add("ContractPrefix", query.ContractId + "%");
        }

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Fetch one extra row instead of a second COUNT(*) query: cheaper, and all we need
        // is "is there more", not an exact total.
        parameters.Add("Limit", pageSize + 1);
        parameters.Add("Offset", (page - 1) * pageSize);

        var sql = PaymentQuerySql.SelectBase
            + (predicates.Count > 0 ? " where " + string.Join(" and ", predicates) : "")
            + " order by received_at desc, id desc limit @Limit offset @Offset;";

        var cmd = new CommandDefinition(sql, parameters, cancellationToken: ct);
        var rows = (await db.Connection.QueryAsync<PaymentListItem>(cmd)).AsList();

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return (rows, hasMore);
    }

    public async Task<PaymentDetail?> FindByIdAsync(long id, CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentQuerySql.FindById, new { Id = id }, cancellationToken: ct);
        return await db.Connection.QuerySingleOrDefaultAsync<PaymentDetail>(cmd);
    }

    public async Task<PaymentStats> GetStatsAsync(CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentQuerySql.Stats, cancellationToken: ct);
        return await db.Connection.QuerySingleAsync<PaymentStats>(cmd);
    }

    public async Task<IReadOnlyList<string>> ListContractIdsAsync(CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentQuerySql.ListContractIds, cancellationToken: ct);
        return (await db.Connection.QueryAsync<string>(cmd)).AsList();
    }
}
