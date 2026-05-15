-- V013: Phase 57 inter-agent coordination via PM agents.
--
-- `coordination_events` is a mailbox table for typed messages between
-- agent groups (teams / swarms / ephemeral sets). Each row represents a
-- single coordination event dispatched by one group's PM agent and
-- targeted at another.
--
-- `group_pm_assignments` maps each agent group to its PM agent template
-- so the coordinator knows which PM to instantiate per group.

-- ── coordination_events ────────────────────────────────────────────────
CREATE TABLE coordination_events (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    source_group_id   TEXT NOT NULL,
    target_group_id   TEXT NOT NULL,
    event_type        TEXT NOT NULL,
    payload           TEXT NOT NULL DEFAULT '{}',
    status            TEXT NOT NULL DEFAULT 'pending',
    workspace_id      TEXT NOT NULL,
    project_id        TEXT,
    created_at        TEXT NOT NULL DEFAULT (datetime('now')),
    delivered_at      TEXT,
    acknowledged_at   TEXT
);

CREATE INDEX ix_coordination_events_target  ON coordination_events(target_group_id, status);
CREATE INDEX ix_coordination_events_source  ON coordination_events(source_group_id);
CREATE INDEX ix_coordination_events_ws      ON coordination_events(workspace_id);

-- ── group_pm_assignments ───────────────────────────────────────────────
CREATE TABLE group_pm_assignments (
    group_id      TEXT PRIMARY KEY,
    group_type    TEXT NOT NULL,
    pm_template   TEXT NOT NULL DEFAULT 'pm-coordinator',
    workspace_id  TEXT NOT NULL,
    project_id    TEXT,
    created_at    TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX ix_group_pm_assignments_ws ON group_pm_assignments(workspace_id);
