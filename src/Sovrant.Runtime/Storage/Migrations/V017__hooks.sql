-- V017: Hook definitions previously loaded from .sovrant/hooks.json
-- One row per hook. Web/Desktop UI is the canonical edit surface.

CREATE TABLE hooks (
    hook_id      TEXT PRIMARY KEY,
    event        TEXT    NOT NULL,
    command      TEXT    NOT NULL,
    matcher_tool TEXT,
    matcher_glob TEXT,
    timeout_ms   INTEGER NOT NULL DEFAULT 30000,
    enabled      INTEGER NOT NULL DEFAULT 1,
    created_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT    NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX hooks_event_enabled_idx ON hooks(event) WHERE enabled = 1;
