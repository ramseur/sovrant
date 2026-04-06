-- V003: Memory system — session summaries, learned patterns, instincts

-- ── Session summaries ───────────────────────────────────────────────────────
CREATE TABLE session_summaries (
    session_id         TEXT PRIMARY KEY,
    project            TEXT NOT NULL,
    workspace_id       TEXT,
    started_at         TEXT NOT NULL,
    ended_at           TEXT NOT NULL,
    tasks              TEXT NOT NULL DEFAULT '[]',
    tools_used         TEXT NOT NULL DEFAULT '[]',
    files_modified     TEXT NOT NULL DEFAULT '[]',
    outcome            TEXT NOT NULL DEFAULT 'Unknown',
    total_input_tokens INTEGER NOT NULL DEFAULT 0,
    total_output_tokens INTEGER NOT NULL DEFAULT 0,
    turn_count         INTEGER NOT NULL DEFAULT 0,
    error_count        INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_session_summaries_project ON session_summaries(project);

-- ── Learned patterns ────────────────────────────────────────────────────────
CREATE TABLE learned_patterns (
    id             TEXT PRIMARY KEY,
    pattern        TEXT NOT NULL,
    project        TEXT NOT NULL,
    source_session TEXT,
    confidence     REAL NOT NULL DEFAULT 0.5,
    created_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    last_used      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_learned_patterns_project ON learned_patterns(project);

-- ── Instincts ───────────────────────────────────────────────────────────────
CREATE TABLE instincts (
    id         TEXT PRIMARY KEY,
    trigger    TEXT NOT NULL,
    action     TEXT NOT NULL,
    confidence REAL NOT NULL DEFAULT 0.5,
    evidence   TEXT NOT NULL DEFAULT '[]',
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
