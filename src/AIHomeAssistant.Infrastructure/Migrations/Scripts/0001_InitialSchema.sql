-- Phase 1 tables — naming: snake_case, ISO 8601 UTC timestamps

CREATE TABLE IF NOT EXISTS command_log (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    created_at       TEXT    NOT NULL,   -- ISO 8601 UTC e.g. '2026-03-25T21:30:00Z'
    transcript       TEXT,
    resolved_intent  TEXT,
    entity_id        TEXT,
    ha_response_code INTEGER,
    latency_ms       INTEGER,
    audio_profile    TEXT,
    error_code       TEXT
);

CREATE TABLE IF NOT EXISTS shopping_list (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    item        TEXT    NOT NULL,
    created_at  TEXT    NOT NULL,
    cleared_at  TEXT
);

CREATE TABLE IF NOT EXISTS comfort_preferences (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    zone        TEXT    NOT NULL,
    sensation   TEXT    NOT NULL,   -- 'hot', 'cold', 'comfortable'
    temperature REAL    NOT NULL,
    recorded_at TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS app_config (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
