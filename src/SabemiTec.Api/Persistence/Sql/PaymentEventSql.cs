namespace SabemiTec.Api.Persistence.Sql;

internal static class PaymentEventSql
{
    // ON CONFLICT DO NOTHING + RETURNING: null = duplicate, id = inserted now.
    // Idempotency lives here, in the unique constraint — not in a prior SELECT.
    public const string InsertIfNotExists = """
        insert into payment_event
            (transaction_id, contract_id, amount, payment_date, payment_status,
             payload, is_valid, validation_error, processing_status)
        values
            (@TransactionId, @ContractId, @Amount, @PaymentDate,
             @PaymentStatus, @Payload::jsonb, @IsValid, @ValidationError, @ProcessingStatus)
        on conflict (transaction_id) do nothing
        returning id;
        """;

    public const string FindByTransactionId = """
        select id, transaction_id, processing_status, received_at
        from payment_event
        where transaction_id = @TransactionId;
        """;

    // FOR UPDATE SKIP LOCKED: each worker locks what it grabbed; the others skip instead of
    // blocking. The lease clause (processing_status=1 with an expired locked_at) recovers
    // items left behind by a crash.
    public const string ClaimBatch = """
        update payment_event e
        set    processing_status = 1, attempts = e.attempts + 1, locked_at = now()
        from ( select id from payment_event
                where (processing_status = 0 and available_at <= now())
                   or (processing_status = 1 and locked_at < now() - @LeaseSeconds * interval '1 second')
                order by available_at, id
                for update skip locked
                limit @BatchSize ) as c
        where  e.id = c.id
        returning e.id, e.transaction_id, e.contract_id, e.amount, e.payment_date,
                  e.payment_status, e.attempts;
        """;

    // Conditional on the lease: if another worker already reclaimed it (lease expired and
    // claimed elsewhere), affected = 0 and the caller undoes the contract upsert instead of
    // applying it twice.
    public const string MarkProcessed = """
        update payment_event
        set processing_status = 2, processed_at = now(), last_error = null
        where id = @Id and processing_status = 1;
        """;

    public const string MarkFailedOrDeadLettered = """
        update payment_event
        set processing_status = case when attempts >= @MaxAttempts then 3 else 0 end,
            available_at = now() + @RetryDelaySeconds * interval '1 second',
            locked_at = null,
            last_error = @Error
        where id = @Id;
        """;
}
