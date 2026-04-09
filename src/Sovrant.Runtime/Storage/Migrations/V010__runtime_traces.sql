-- V010: Phase 51 engine layer — runtime traces and mission scratchpad.
--
-- `runtime_traces` is the append-only structured reasoning trace written by
-- the Phase 51 IExecutor. Every state transition (plan_created, step_started,
-- step_completed, step_failed, replan_requested, run_completed) is committed
-- here BEFORE the corresponding side effect runs, so a crash mid-step leaves
-- a recoverable trail. EngineRecovery walks this table on startup to resume
-- any executor that was in-flight at the moment of crash.
--
-- `mission_scratchpad` is the typed, append-only shared store used by
-- parallel sub-agents within one mission to write findings the next plan
-- wave can read. Keyed by mission + step, so a mission's sub-agents can
-- see each other's intermediate outputs without going through the
-- orchestrator as an integration point.
--
-- Both tables are workspace- and project-scoped so Phase 53 artifact
-- cleanup and Phase 47 workspace export pick them up by the same keys
-- they already use for sessions, memory, and swarm events.

-- ── runtime_traces ──────────────────────────────────────────────────────────
CREATE TABLE runtime_traces (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    runtime_run_id TEXT NOT NULL,
    plan_id        TEXT NOT NULL,
    plan_version   INTEGER NOT NULL,
    step_index     INTEGER,                -- NULL for plan-level entries
    entry_type     TEXT NOT NULL,          -- plan_created | step_started | step_completed | step_failed | replan_requested | run_completed
    payload        TEXT NOT NULL DEFAULT '{}',
    timestamp      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    session_id     TEXT,
    workspace_id   TEXT,
    project_id     TEXT
);

-- Primary lookup path: load the full trace for one runtime run, in order.
CREATE INDEX ix_runtime_traces_run ON runtime_traces(runtime_run_id, id);

-- Recovery lookup: list runs that have a step_started but no matching
-- step_completed/step_failed. The recovery path groups by runtime_run_id
-- and checks for unmatched entry_types, so an index on entry_type helps.
CREATE INDEX ix_runtime_traces_entry_type ON runtime_traces(entry_type);

-- Workspace/project cleanup joins.
CREATE INDEX ix_runtime_traces_workspace ON runtime_traces(workspace_id);
CREATE INDEX ix_runtime_traces_project ON runtime_traces(project_id);

-- ── mission_scratchpad ──────────────────────────────────────────────────────
CREATE TABLE mission_scratchpad (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    mission_id   TEXT NOT NULL,
    step_index   INTEGER NOT NULL,
    agent_id     TEXT,                    -- NULL if written by the executor directly
    namespace    TEXT NOT NULL DEFAULT 'default',
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,           -- JSON payload
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    workspace_id TEXT,
    project_id   TEXT
);

-- Primary read path: load the scratchpad for one mission, optionally
-- filtered to a step or namespace.
CREATE INDEX ix_mission_scratchpad_mission ON mission_scratchpad(mission_id, step_index);

-- Workspace-scoped cleanup.
CREATE INDEX ix_mission_scratchpad_workspace ON mission_scratchpad(workspace_id);
