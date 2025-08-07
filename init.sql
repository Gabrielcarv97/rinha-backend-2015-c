
CREATE UNLOGGED TABLE payment (
    correlationid UUID PRIMARY KEY,
    amount NUMERIC NOT NULL,
    requested TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processorused VARCHAR(10) NOT NULL DEFAULT 'default'
);

CREATE INDEX idx_payment_requested ON payment (requested);
