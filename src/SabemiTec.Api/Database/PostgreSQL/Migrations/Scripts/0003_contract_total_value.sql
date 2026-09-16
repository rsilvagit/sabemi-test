-- Same demo-only spirit as migration 0002: the webhook payload never carries the contract's
-- total value either, only the per-transaction `valor`. Adds a column instead of a new table
-- since it's more master data about the same seeded contract, not a new entity.
alter table contract add column total_value numeric(18,2) null;

update contract set total_value = 6000.00  where contract_id = 'CT-1001';
update contract set total_value = 1200.00  where contract_id = 'CT-1002';
update contract set total_value = 14400.00 where contract_id = 'CT-1003';
update contract set total_value = 2400.00  where contract_id = 'CT-1004';
update contract set total_value = 3000.00  where contract_id = 'CT-1005';
update contract set total_value = 900.00   where contract_id = 'CT-1006';
update contract set total_value = 21600.00 where contract_id = 'CT-1007';
update contract set total_value = 1800.00  where contract_id = 'CT-1008';
