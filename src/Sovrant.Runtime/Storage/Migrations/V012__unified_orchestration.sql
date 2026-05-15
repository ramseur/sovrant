-- V012: Phase 52 unified agent orchestration — collapse Team (in-memory)
-- and Swarm (ephemeral, parallel) into one DB-backed abstraction.
--
-- `teams` is the canonical team record. Each row is a named collection
-- of agent members scoped to a workspace/project. Teams can originate
-- from user creation, LLM conversation, or swarm worker publication.
--
-- `team_members` stores individual members of a team. Each member has
-- a role, optional template reference, system prompt, allowed tools,
-- and model level. Members survive restarts (unlike InMemoryTeamRegistry).
--
-- `agent_runs` is the unified run ledger that tracks every agentic
-- execution — single-shot delegation, swarm wave task, or mission step —
-- regardless of how it was started. Supports parent_run_id for sub-spawns.
--
-- The existing `swarm_events` table is extended with a `kind` column
-- (discriminator for 'swarm' vs 'delegation' vs 'mission-step') and
-- a `run_id` column linking events to agent_runs.

-- ── teams ───────────────────────────────────────────────────────────────────
CREATE TABLE teams (
    team_id      TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    project_id   TEXT,
    name         TEXT NOT NULL,
    description  TEXT,
    origin       TEXT NOT NULL DEFAULT 'user',  -- user | llm-created | swarm-published
    created_by   TEXT NOT NULL,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_teams_workspace ON teams(workspace_id);
CREATE INDEX ix_teams_project   ON teams(project_id);
CREATE INDEX ix_teams_name      ON teams(workspace_id, name);

-- ── team_members ────────────────────────────────────────────────────────────
CREATE TABLE team_members (
    member_id    TEXT PRIMARY KEY,
    team_id      TEXT NOT NULL REFERENCES teams(team_id) ON DELETE CASCADE,
    workspace_id TEXT NOT NULL,
    project_id   TEXT,
    name         TEXT NOT NULL,
    role         TEXT NOT NULL DEFAULT 'general',  -- general | planner | coder | reviewer | tester | researcher
    template     TEXT,                              -- agent template name from Phase 22
    system_prompt TEXT,
    allowed_tools TEXT,                             -- JSON array
    model_level  TEXT,                              -- high | standard | fast
    created_by   TEXT NOT NULL,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    last_used_at TEXT,
    status       TEXT NOT NULL DEFAULT 'active'     -- active | inactive
);

CREATE INDEX ix_team_members_team      ON team_members(team_id);
CREATE INDEX ix_team_members_workspace ON team_members(workspace_id);

-- ── agent_runs ──────────────────────────────────────────────────────────────
CREATE TABLE agent_runs (
    run_id        TEXT PRIMARY KEY,
    parent_run_id TEXT,                             -- for sub-spawns (swarm wave step, mission sub-step)
    team_id       TEXT,                             -- nullable: ad-hoc runs may have no team
    member_id     TEXT,                             -- nullable: ephemeral workers
    workspace_id  TEXT NOT NULL,
    project_id    TEXT,
    user_id       TEXT NOT NULL,
    kind          TEXT NOT NULL,                    -- delegation | swarm-task | mission-step
    status        TEXT NOT NULL DEFAULT 'queued',   -- queued | running | succeeded | failed | cancelled
    started_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    ended_at      TEXT,
    input_tokens  INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd      REAL
);

CREATE INDEX ix_agent_runs_parent    ON agent_runs(parent_run_id);
CREATE INDEX ix_agent_runs_team      ON agent_runs(team_id);
CREATE INDEX ix_agent_runs_workspace ON agent_runs(workspace_id);
CREATE INDEX ix_agent_runs_user      ON agent_runs(user_id);
CREATE INDEX ix_agent_runs_kind      ON agent_runs(kind);
CREATE INDEX ix_agent_runs_status    ON agent_runs(status);

-- ── Extend swarm_events with kind + run_id ──────────────────────────────────
ALTER TABLE swarm_events ADD COLUMN kind TEXT NOT NULL DEFAULT 'swarm';
ALTER TABLE swarm_events ADD COLUMN run_id TEXT;
CREATE INDEX ix_swarm_events_run_id ON swarm_events(run_id);
CREATE INDEX ix_swarm_events_kind   ON swarm_events(kind);
