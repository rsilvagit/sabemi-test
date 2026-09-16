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

    // Which installment this event is, within its own contract: position in the chronological
    // sequence of events received for that contract_id — demo-only, same spirit as
    // contract_type/installments (no payload field carries an installment number either).
    private const string InstallmentNumberWindow =
        "row_number() over (partition by e.contract_id order by e.received_at)";

    // Wrapped as a subquery so `effective_status` becomes a real column the outer WHERE can
    // filter on — Postgres does not let WHERE see a SELECT-list alias (WHERE runs before SELECT).
    // contract_type/installments come from a LEFT JOIN, not the event itself — see migration
    // 0002 (demo-only mocked contract master data, no payload field carries this). Only
    // `contract_id` is ambiguous between the two tables (qualified below); the columns the
    // CASE expressions read (is_valid, processing_status, payment_status) exist only on
    // payment_event, so they need no qualification.
    public static readonly string SelectBase = $"""
        select * from (
            select
                e.id, e.contract_id, e.transaction_id, e.amount, e.payment_date, e.payment_status,
                e.processing_status, e.attempts, e.last_error, e.validation_error, e.received_at, e.processed_at,
                {EffectiveStatusCase} as effective_status,
                {ErrorCategoryCase} as error_category,
                c.contract_type, c.installments, c.total_value,
                {InstallmentNumberWindow} as installment_number
            from payment_event e
            left join contract c on c.contract_id = e.contract_id
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

    // installment_number is a window function over contract_id — it has to see every event
    // for that contract, not just the one row `id = @Id` would filter down to. WHERE runs
    // before window functions in SQL's logical order, so filtering by id has to happen in an
    // outer query wrapping the window instead of alongside it.
    public static readonly string FindById = $"""
        select * from (
            select
                e.id, e.contract_id, e.transaction_id, e.amount, e.payment_date, e.payment_status,
                e.processing_status, e.attempts, e.last_error, e.validation_error, e.received_at, e.processed_at,
                e.payload,
                {EffectiveStatusCase} as effective_status,
                {ErrorCategoryCase} as error_category,
                c.contract_type, c.installments, c.total_value,
                {InstallmentNumberWindow} as installment_number
            from payment_event e
            left join contract c on c.contract_id = e.contract_id
        ) events
        where id = @Id;
        """;

    public static readonly string ListContractIds = "select contract_id from contract order by contract_id;";
}
