-- Phase 1 Extensions + Phase 2 tables — alert_state, consent_record

CREATE TABLE IF NOT EXISTS alert_state (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    sensor_id           TEXT    NOT NULL,
    triggered_at        TEXT    NOT NULL,   -- ISO 8601 UTC
    acknowledged_at     TEXT,               -- NULL until acknowledged
    cleared_at          TEXT,               -- NULL until resolved
    status              TEXT    NOT NULL DEFAULT 'active',  -- active | acknowledged | cleared
    notification_failed INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS consent_record (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    member_id       TEXT    NOT NULL UNIQUE,
    member_name     TEXT    NOT NULL,
    azure_person_id TEXT    NOT NULL,
    granted_at      TEXT    NOT NULL,   -- ISO 8601 UTC
    revoked_at      TEXT,               -- NULL while active
    active          INTEGER NOT NULL DEFAULT 1
);
