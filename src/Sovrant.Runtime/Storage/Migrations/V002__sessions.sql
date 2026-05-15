-- V002: Sessions, session entries (with FTS5), and token usage

-- ── Sessions ────────────────────────────────────────────────────────────────
CREATE TABLE sessions (
    session_id   TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL DEFAULT '',
    workspace_id TEXT,
    project_id   TEXT,
    model        TEXT,
    status       TEXT NOT NULL DEFAULT 'active',
    started_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    ended_at     TEXT,
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_sessions_user ON sessions(user_id);
CREATE INDEX ix_sessions_status ON sessions(status);

-- ── Session entries ─────────────────────────────────────────────────────────
CREATE TABLE session_entries (
    entry_id     INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id   TEXT NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    entry_uid    TEXT NOT NULL,
    timestamp    TEXT NOT NULL,
    role         TEXT NOT NULL,
    content      TEXT NOT NULL,
    model        TEXT,
    input_tokens INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    tool_name    TEXT,
    tool_use_id  TEXT,
    is_error     INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_session_entries_session ON session_entries(session_id);

-- ── Full-text search on session content ─────────────────────────────────────
CREATE VIRTUAL TABLE session_entries_fts USING fts5(
    content,
    content=session_entries,
    content_rowid=entry_id
);

-- Auto-sync triggers for FTS
CREATE TRIGGER session_entries_ai AFTER INSERT ON session_entries BEGIN
    INSERT INTO session_entries_fts(rowid, content) VALUES (new.entry_id, new.content);
END;

CREATE TRIGGER session_entries_ad AFTER DELETE ON session_entries BEGIN
    INSERT INTO session_entries_fts(session_entries_fts, rowid, content) VALUES ('delete', old.entry_id, old.content);
END;

CREATE TRIGGER session_entries_au AFTER UPDATE ON session_entries BEGIN
    INSERT INTO session_entries_fts(session_entries_fts, rowid, content) VALUES ('delete', old.entry_id, old.content);
    INSERT INTO session_entries_fts(rowid, content) VALUES (new.entry_id, new.content);
END;

-- ── Token usage ─────────────────────────────────────────────────────────────
CREATE TABLE token_usage (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id   TEXT NOT NULL,
    user_id      TEXT NOT NULL DEFAULT '',
    workspace_id TEXT,
    project_id   TEXT,
    model        TEXT NOT NULL,
    input_tokens INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd     REAL,
    recorded_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_token_usage_session ON token_usage(session_id);
CREATE INDEX ix_token_usage_user ON token_usage(user_id);
