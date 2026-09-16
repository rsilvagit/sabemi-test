using SabemiTec.Api.Enum;

namespace SabemiTec.Api.Database.PostgreSQL.Sql;

internal static class PaymentQuerySql
{
    // effective_status resolves the ambiguity between payment_status (the bank's word) and
    // processing_status (ours) into the single filter the dashboard needs — see the plan.
    private static readonly string EffectiveStatusCase = $"""
        case
            when not is_valid or processing_status = {ProcessingStatusEnum.DeadLettered.Id} then 'Error'
            when processing_status in ({ProcessingStatusEnum.Pending.Id}, {ProcessingStatusEnum.Locked.Id}) then 'Pending'
            when processing_status = {ProcessingStatusEnum.Processed.Id} and payment_status = '{BankPaymentStatusEnum.Paid.Name}' then 'Success'
            else 'Error'
        end
        """;

    // Only meaningful when effective_status = 'Error' — distinguishes a malformed/incomplete
    // payload (never reached the bank's business outcome) from a payload the bank fully
    // processed and reported as failed (or that died after exhausting retries). The PDF's
    // "alerta visual claro" requirement covers both, but they are different situations for an
    // operator reading the dashboard, so the label needs to say which one it is.
    private static readonly string ErrorCategoryCase = $"""
        case
            when not is_valid then 'Validation'
            when processing_status = {ProcessingStatusEnum.DeadLettered.Id}
                or (processing_status = {ProcessingStatusEnum.Processed.Id} and payment_status <> '{BankPaymentStatusEnum.Paid.Name}')
                then 'PaymentFailure'
            else null
        end
        """;

    // Wrapped as a subquery so `effective_status` becomes a real column the outer WHERE can
    // filter on — Postgres does not let WHERE see a SELECT-list alias (WHERE runs before SELECT).
    public static readonly string SelectBase = $"""
        select * from (
            select
                id, transaction_id, contract_id, amount, payment_date, payment_status,
                processing_status, attempts, last_error, validation_error, received_at, processed_at,
                {EffectiveStatusCase} as effective_status,
                {ErrorCategoryCase} as error_category
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
            {EffectiveStatusCase} as effective_status,
            {ErrorCategoryCase} as error_category
        from payment_event
        where id = @Id;
        """;
}
