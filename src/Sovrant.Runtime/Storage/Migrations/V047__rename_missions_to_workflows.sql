-- V047: rename the mission layer to "workflows" — roadmap item, no behavior
-- change. Purely a naming pass: the mission/workflow entity, its event
-- journal, and its scratchpad keep the exact same shape and semantics as
-- V011 (missions/mission_events) + V010 (mission_scratchpad) + V030
-- (is_private) defined them. Those files are untouched; this is additive.
--
-- Three tables rename, not two — mission_scratchpad (V010) is easy to miss
-- since it wasn't introduced alongside missions/mission_events in V011.
--
-- SQLite RENAME TABLE/RENAME COLUMN (3.25+) perform an in-place rename of
-- the physical schema object — existing rows and data are untouched, only
-- names change. This is one-way: there is no realistic "undo" migration
-- once rows have been written under the new names, so this must be tested
-- against a copy of a real database, not just an empty one, before it ships.

-- ── missions → workflows ──────────────────────────────────────────────────
ALTER TABLE missions RENAME TO workflows;

DROP INDEX ix_missions_status;
DROP INDEX ix_missions_workspace;
DROP INDEX ix_missions_project;
DROP INDEX ix_missions_owner;
DROP INDEX ix_missions_is_private;

CREATE INDEX ix_workflows_status     ON workflows(status);
CREATE INDEX ix_workflows_workspace  ON workflows(workspace_id);
CREATE INDEX ix_workflows_project    ON workflows(project_id);
CREATE INDEX ix_workflows_owner      ON workflows(owner_user_id);
CREATE INDEX ix_workflows_is_private ON workflows(is_private);

-- ── mission_events → workflow_events ────────────────────────────────────
ALTER TABLE mission_events RENAME TO workflow_events;
ALTER TABLE workflow_events RENAME COLUMN mission_id TO workflow_id;

DROP INDEX ix_mission_events_mission;
DROP INDEX ix_mission_events_workspace;

CREATE INDEX ix_workflow_events_workflow  ON workflow_events(workflow_id, id);
CREATE INDEX ix_workflow_events_workspace ON workflow_events(workspace_id);

-- ── mission_scratchpad → workflow_scratchpad ────────────────────────────
ALTER TABLE mission_scratchpad RENAME TO workflow_scratchpad;
ALTER TABLE workflow_scratchpad RENAME COLUMN mission_id TO workflow_id;

DROP INDEX ix_mission_scratchpad_mission;
DROP INDEX ix_mission_scratchpad_workspace;

CREATE INDEX ix_workflow_scratchpad_workflow  ON workflow_scratchpad(workflow_id, step_index);
CREATE INDEX ix_workflow_scratchpad_workspace ON workflow_scratchpad(workspace_id);

-- Deliberately NOT touched: the string *values* already persisted in
-- workflow_events.event_type ('mission_created', 'plan_revised', etc.).
-- Those are historical journal payload data, not schema — rewriting them
-- for a cosmetic rename adds risk (touching every historical row) for no
-- real benefit. The C# constant *names* that produce these values do get
-- renamed (MissionEventTypes → WorkflowEventTypes), but their string
-- values stay exactly as they were.
