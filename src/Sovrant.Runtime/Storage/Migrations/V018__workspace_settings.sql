-- V018: Workspace-scoped settings — budgets, session TTL/cap, and other
-- runtime-mutable knobs that previously lived in env vars only.
--
-- Convention: workspace_id = '' means "global / server default". Readers
-- look up the workspace-specific row first; if missing, fall back to the
-- global row; env-var overrides still win at the call site.

CREATE TABLE workspace_settings (
    workspace_id TEXT NOT NULL DEFAULT '',
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (workspace_id, key)
);

CREATE INDEX workspace_settings_key_idx ON workspace_settings(key);
