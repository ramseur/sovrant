-- V011: Phase 51 mission layer — durable mission state and mission event journal.
--
-- The mission layer sits on top of the Phase 51 engine layer: a mission
-- is a long-lived goal that the system pursues autonomously, potentially
-- over many engine runs, with re-planning, acceptance gates, and an
-- append-only event journal. The engine layer (runtime_traces) handles
-- the in-flight per-run trace; the mission layer adds the higher-level
-- "what is the mission currently doing and why" state above it.
--
-- `missions` is the canonical mission record. Each row is one mission,
-- with its goal text, current status, current plan snapshot (JSON),
-- and workspace/project scoping for cleanup and access control.
--
-- `mission_events` is the append-only journal. Every state transition
-- (mission_created, plan_revised, run_started, run_completed,
--  acceptance_approved, acceptance_rejected, paused, resumed,
--  completed, failed) gets one row, so the mission history is fully
-- reconstructable from the journal alone without trusting the mutable
-- fields in `missions`.

-- ── missions ────────────────────────────────────────────────────────────────
CREATE TABLE missions (
    id            TEXT PRIMARY KEY,
    goal          TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'planning',  -- planning | running | awaiting_human | completed | failed | cancelled
    plan_json     TEXT NOT NULL DEFAULT '{}',
    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    completed_at  TEXT,
    session_id    TEXT,
    workspace_id  TEXT,
    project_id    TEXT,
    owner_user_id TEXT
);

CREATE INDEX ix_missions_status    ON missions(status);
CREATE INDEX ix_missions_workspace ON missions(workspace_id);
CREATE INDEX ix_missions_project   ON missions(project_id);
CREATE INDEX ix_missions_owner     ON missions(owner_user_id);

-- ── mission_events ──────────────────────────────────────────────────────────
CREATE TABLE mission_events (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    mission_id   TEXT NOT NULL,
    event_type   TEXT NOT NULL,           -- mission_created | plan_revised | run_started | run_completed | acceptance_* | paused | resumed | completed | failed
    payload      TEXT NOT NULL DEFAULT '{}',
    timestamp    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    workspace_id TEXT,
    project_id   TEXT
);

CREATE INDEX ix_mission_events_mission   ON mission_events(mission_id, id);
CREATE INDEX ix_mission_events_workspace ON mission_events(workspace_id);
