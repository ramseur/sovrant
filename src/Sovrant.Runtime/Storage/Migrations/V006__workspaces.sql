-- V006: Workspace activation — workspace_memory table + indexes for workspace scoping

-- ── Workspace memory (Phase 35) ────────────────────────────────────────────
CREATE TABLE workspace_memory (
    memory_id    TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id) ON DELETE CASCADE,
    layer        TEXT NOT NULL,  -- 'summary', 'pattern', 'instinct'
    content      TEXT NOT NULL,
    confidence   REAL,
    project_id   TEXT,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_workspace_memory_workspace ON workspace_memory(workspace_id);
CREATE INDEX ix_workspace_memory_layer     ON workspace_memory(workspace_id, layer);

-- ── Add workspace indexes to existing tables for scoped queries ────────────
CREATE INDEX IF NOT EXISTS ix_sessions_workspace       ON sessions(workspace_id);
CREATE INDEX IF NOT EXISTS ix_token_usage_workspace     ON token_usage(workspace_id);
CREATE INDEX IF NOT EXISTS ix_session_summaries_workspace ON session_summaries(workspace_id);
CREATE INDEX IF NOT EXISTS ix_audit_governance_workspace ON audit_governance(workspace_id);
CREATE INDEX IF NOT EXISTS ix_audit_bash_workspace       ON audit_bash(workspace_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_workspace     ON swarm_events(workspace_id);
CREATE INDEX IF NOT EXISTS ix_eval_runs_workspace        ON eval_runs(workspace_id);
