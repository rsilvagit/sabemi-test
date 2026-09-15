namespace SabemiTec.Api.Persistence.Sql;

internal static class PaymentQuerySql
{
    // effective_status resolves the ambiguity between payment_status (the bank's word) and
    // processing_status (ours) into the single filter the dashboard needs — see the plan.
    private const string EffectiveStatusCase = """
        case
            when not is_valid or processing_status = 3 then 'Error'
            when processing_status in (0, 1) then 'Pending'
            when processing_status = 2 and payment_status = 'PAGO' then 'Success'
            else 'Error'
        end
        """;

    // Wrapped as a subquery so `effective_status` becomes a real column the outer WHERE can
    // filter on — Postgres does not let WHERE see a SELECT-list alias (WHERE runs before SELECT).
    public static readonly string SelectBase = $"""
        select * from (
            select
                id, transaction_id, contract_id, amount, payment_date, payment_status,
                processing_status, attempts, last_error, validation_error, received_at, processed_at,
                {EffectiveStatusCase} as effective_status
            from payment_event
        ) events
        """;

    // Reuses the same case expression per bucket — one table scan, four conditional
    // aggregates. Cheap enough to run on every dashboard poll.
    public static readonly string Stats = $"""
        select
            count(*) as total,
            count(*) filter (where {EffectiveStatusCase} = 'Success') as success,
            count(*) filter (where {EffectiveStatusCase} = 'Error') as error,
            count(*) filter (where {EffectiveStatusCase} = 'Pending') as pending
        from payment_event;
        """;

    public static readonly string FindById = $"""
        select
            id, transaction_id, contract_id, amount, payment_date, payment_status,
            processing_status, attempts, last_error, validation_error, received_at, processed_at,
            payload,
            {EffectiveStatusCase} as effective_status
        from payment_event
        where id = @Id;
        """;
}
