namespace SabemiTec.Api.Database.PostgreSQL.Sql;

internal static class ContractStatusSql
{
    // Atomic accumulation in SQL itself — never read into memory, sum in C#, and write back.
    // GREATEST + CASE handle out-of-order events: an old webhook that arrives late still
    // adds to the total but never overwrites the "latest status" with a stale one.
    public const string Upsert = """
        insert into contract_status as cs
            (contract_id, total_paid, payments_count, failed_count,
             last_payment_at, last_transaction_id, last_status, updated_at)
        values (@ContractId, @Amount, @SuccessDelta, @FailedDelta,
                @PaymentDate, @TransactionId, @PaymentStatus, now())
        on conflict (contract_id) do update set
            total_paid     = cs.total_paid     + excluded.total_paid,
            payments_count = cs.payments_count + excluded.payments_count,
            failed_count   = cs.failed_count   + excluded.failed_count,
            last_payment_at = greatest(cs.last_payment_at, excluded.last_payment_at),
            last_transaction_id = case
                when excluded.last_payment_at >= coalesce(cs.last_payment_at, '-infinity'::timestamptz)
                then excluded.last_transaction_id else cs.last_transaction_id end,
            last_status = case
                when excluded.last_payment_at >= coalesce(cs.last_payment_at, '-infinity'::timestamptz)
                then excluded.last_status else cs.last_status end,
            updated_at = now();
        """;
}
