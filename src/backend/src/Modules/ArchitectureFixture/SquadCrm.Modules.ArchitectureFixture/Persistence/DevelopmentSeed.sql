-- CRM-203 development/test seed. Synthetic architecture-fixture data only.
-- This file is module-owned and is never executed by application startup.
INSERT INTO architecture_fixture.persistence_probe (id, label, recorded_at_utc)
VALUES (
    '00000000-0000-0000-0000-000000000203',
    'synthetic-development-seed',
    '2026-01-01T00:00:00Z')
ON CONFLICT (id) DO UPDATE
SET label = EXCLUDED.label,
    recorded_at_utc = EXCLUDED.recorded_at_utc;
