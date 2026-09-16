-- Same demo-only spirit as migrations 0002/0003. Seguro contracts were seeded with
-- installments = null (single flat premium) — the user pointed out this made Seguro the
-- only contract type never showing a parcela N/Total in the dashboard. Gives Seguro an
-- installment plan too, values chosen so total_value / installments divides evenly.
update contract set installments = 12 where contract_id = 'CT-1002'; -- 1200 / 12 = 100.00
update contract set installments = 24 where contract_id = 'CT-1004'; -- 2400 / 24 = 100.00
update contract set installments = 6  where contract_id = 'CT-1006'; -- 900  / 6  = 150.00
update contract set installments = 12 where contract_id = 'CT-1008'; -- 1800 / 12 = 150.00
