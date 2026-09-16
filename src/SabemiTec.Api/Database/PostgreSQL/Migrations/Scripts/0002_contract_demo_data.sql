-- Demo-only contract master data. The webhook payload never carries contract type or an
-- installment plan (id_transacao, id_contrato, valor, data_pagamento, status is all the
-- partner bank sends) — this table is a mocked seed, not fed by any real event, purely to
-- make "liquidação de seguros ou parcelas de empréstimos" (the assessment's own framing)
-- readable on the dashboard instead of a bare contract_id. A real system would source this
-- from an actual contracts service.
create table contract (
    contract_id   varchar(128)  primary key,
    contract_type varchar(16)   not null,  -- 'Emprestimo' | 'Seguro'
    installments  int               null   -- only loans carry an installment plan
);

insert into contract (contract_id, contract_type, installments) values
    ('CT-1001', 'Emprestimo', 12),
    ('CT-1002', 'Seguro',     null),
    ('CT-1003', 'Emprestimo', 24),
    ('CT-1004', 'Seguro',     null),
    ('CT-1005', 'Emprestimo', 6),
    ('CT-1006', 'Seguro',     null),
    ('CT-1007', 'Emprestimo', 36),
    ('CT-1008', 'Seguro',     null);
