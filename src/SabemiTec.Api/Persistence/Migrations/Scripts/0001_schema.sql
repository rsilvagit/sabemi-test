-- Raw event log = the processing queue itself (Inbox / Idempotent Receiver).
create table payment_event (
    id                bigserial     primary key,
    transaction_id    varchar(128)  not null,
    contract_id       varchar(128)      null,
    amount            numeric(18,2)     null,
    payment_date      timestamptz       null,
    payment_status    varchar(32)       null,   -- partner bank vocabulary (PAGO/FALHA)
    payload           jsonb         not null,   -- original body, verbatim
    is_valid          boolean       not null,
    validation_error  text              null,
    received_at       timestamptz   not null default now(),

    -- processing state (the queue)
    processing_status smallint      not null default 0,  -- 0 pending 1 processing 2 done 3 dead
    attempts          int           not null default 0,
    available_at      timestamptz   not null default now(),
    locked_at         timestamptz       null,
    processed_at      timestamptz       null,
    last_error        text              null,

    constraint uq_payment_event_transaction unique (transaction_id)
);

create index ix_payment_event_claimable on payment_event (available_at, id)
    where processing_status in (0, 1);
create index ix_payment_event_contract    on payment_event (contract_id);
create index ix_payment_event_received_at on payment_event (received_at desc);

-- Contract status: derived projection, accumulated incrementally.
create table contract_status (
    contract_id         varchar(128)  primary key,
    total_paid          numeric(18,2) not null default 0,
    payments_count      int           not null default 0,
    failed_count         int           not null default 0,
    last_payment_at      timestamptz       null,
    last_transaction_id  varchar(128)      null,
    last_status           varchar(32)       null,
    updated_at            timestamptz   not null default now()
);
