CREATE TABLE IF NOT EXISTS tenders (
    id TEXT PRIMARY KEY,
    status TEXT NOT NULL,
    cpv_code TEXT NOT NULL,
    procuring_entity_name TEXT NOT NULL,
    expected_amount NUMERIC(18,2) NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NULL,
    date_modified TIMESTAMPTZ NULL,
    raw_json JSONB NOT NULL,
    imported_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS tender_contracts (
    id BIGSERIAL PRIMARY KEY,
    tender_id TEXT NOT NULL REFERENCES tenders(id) ON DELETE CASCADE,
    contract_external_id TEXT NULL,
    amount NUMERIC(18,2) NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS tender_suppliers (
    id BIGSERIAL PRIMARY KEY,
    tender_id TEXT NOT NULL REFERENCES tenders(id) ON DELETE CASCADE,
    supplier_name TEXT NOT NULL,
    award_external_id TEXT NULL,
    amount NUMERIC(18,2) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_tenders_cpv_status_created ON tenders (cpv_code, status, created_at);
CREATE INDEX IF NOT EXISTS ix_tenders_procuring_entity ON tenders (procuring_entity_name);
CREATE INDEX IF NOT EXISTS ix_contracts_tender_id ON tender_contracts (tender_id);
CREATE INDEX IF NOT EXISTS ix_suppliers_name ON tender_suppliers (supplier_name);
CREATE INDEX IF NOT EXISTS ix_tenders_raw_json_gin ON tenders USING GIN (raw_json jsonb_path_ops);

CREATE OR REPLACE VIEW tender_contract_totals AS
SELECT
    t.id AS tender_id,
    t.procuring_entity_name,
    t.expected_amount,
    COALESCE(SUM(c.amount), 0) AS contracts_amount,
    t.expected_amount - COALESCE(SUM(c.amount), 0) AS saving
FROM tenders t
LEFT JOIN tender_contracts c ON c.tender_id = t.id
GROUP BY t.id, t.procuring_entity_name, t.expected_amount;
