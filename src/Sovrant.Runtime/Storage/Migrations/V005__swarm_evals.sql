-- V005: Swarm events and eval run/result persistence

-- ── Swarm events ────────────────────────────────────────────────────────────
CREATE TABLE swarm_events (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    swarm_id     TEXT NOT NULL,
    event_type   TEXT NOT NULL,
    agent_id     TEXT,
    workspace_id TEXT,
    project_id   TEXT,
    payload      TEXT NOT NULL DEFAULT '{}',
    timestamp    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_swarm_events_swarm ON swarm_events(swarm_id);

-- ── Eval runs ───────────────────────────────────────────────────────────────
CREATE TABLE eval_runs (
    run_id           TEXT PRIMARY KEY,
    suite_name       TEXT NOT NULL,
    workspace_id     TEXT,
    started_at       TEXT NOT NULL,
    duration_seconds REAL NOT NULL DEFAULT 0,
    pass_rate        REAL NOT NULL DEFAULT 0,
    pass_at_1_rate   REAL NOT NULL DEFAULT 0,
    total_passed     INTEGER NOT NULL DEFAULT 0,
    total_failed     INTEGER NOT NULL DEFAULT 0,
    total_skipped    INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_eval_runs_suite ON eval_runs(suite_name);

-- ── Eval results (per eval within a run) ────────────────────────────────────
CREATE TABLE eval_results (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    run_id           TEXT NOT NULL REFERENCES eval_runs(run_id) ON DELETE CASCADE,
    eval_name        TEXT NOT NULL,
    category         TEXT NOT NULL,
    grader_type      TEXT NOT NULL,
    passed           INTEGER NOT NULL DEFAULT 0,
    pass_at_1        INTEGER NOT NULL DEFAULT 0,
    pass_count       INTEGER NOT NULL DEFAULT 0,
    attempt_count    INTEGER NOT NULL DEFAULT 0,
    average_score    REAL,
    duration_seconds REAL NOT NULL DEFAULT 0,
    skipped          INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX ix_eval_results_run ON eval_results(run_id);
