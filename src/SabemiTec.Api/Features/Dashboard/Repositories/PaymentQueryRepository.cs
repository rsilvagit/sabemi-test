using Dapper;
using SabemiTec.Api.Database.PostgreSQL;
using SabemiTec.Api.Database.PostgreSQL.Sql;

namespace SabemiTec.Api.Features.Dashboard.Repositories;

/// <summary>Read-only — no transaction here on purpose. Dapper opens/closes the connection
/// per call on its own; nothing here needs it kept open across calls.</summary>
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

        if (!string.IsNullOrWhiteSpace(query.ContractType))
        {
            predicates.Add("contract_type = @ContractType");
            parameters.Add("ContractType", query.ContractType);
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

    public async Task<IReadOnlyList<Contract>> ListContractsAsync(CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentQuerySql.ListContracts, cancellationToken: ct);
        return (await db.Connection.QueryAsync<Contract>(cmd)).AsList();
    }

    public async Task<(IReadOnlyList<InvalidPaymentEvent> Items, bool HasMore)> SearchInvalidAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var parameters = new { Limit = pageSize + 1, Offset = (page - 1) * pageSize };
        var cmd = new CommandDefinition(PaymentQuerySql.SelectInvalid, parameters, cancellationToken: ct);
        var rows = (await db.Connection.QueryAsync<InvalidPaymentEvent>(cmd)).AsList();

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        return (rows, hasMore);
    }

    public async Task<long> CountInvalidAsync(CancellationToken ct)
    {
        var cmd = new CommandDefinition(PaymentQuerySql.CountInvalid, cancellationToken: ct);
        return await db.Connection.QuerySingleAsync<long>(cmd);
    }
}
