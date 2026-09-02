-- Sovrant initial schema for Supabase deployments.
-- Mirrors V001–V043 SQLite migrations. Safe to run multiple times (idempotent).
-- For standalone PostgreSQL use db/postgres/PostgresSchema.sql instead.
--
-- Timestamps are stored as TEXT (ISO 8601) for wire-compatibility with SQLite stores.
-- BYTEA used for encrypted blobs (credentials table).
-- V035/V037 (built-in knowledge seed data) are handled by the app at startup, not here.
--
-- NOTE: user_id remains the GoTrue UUID (auth.users.id::TEXT) in Supabase mode.
-- V043's PK rewrite (usr_{hex} → email) applies to SQLite standalone deployments only.

-- ── V001 Foundation ───────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS users (
    user_id       TEXT PRIMARY KEY,
    email         TEXT,
    role          TEXT NOT NULL DEFAULT 'user',
    team          TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    password_hash TEXT,
    created_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    CONSTRAINT users_email_unique UNIQUE (email)
);

CREATE TABLE IF NOT EXISTS workspaces (
    workspace_id TEXT PRIMARY KEY,
    type         TEXT NOT NULL DEFAULT 'personal',
    name         TEXT NOT NULL,
    slug         TEXT NOT NULL,
    owner_id     TEXT NOT NULL REFERENCES users(user_id),
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    CONSTRAINT workspaces_slug_unique UNIQUE (slug)
);

CREATE TABLE IF NOT EXISTS workspace_members (
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    role         TEXT NOT NULL DEFAULT 'member',
    joined_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (workspace_id, user_id)
);

CREATE TABLE IF NOT EXISTS workspace_config (
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,
    PRIMARY KEY (workspace_id, key)
);

CREATE TABLE IF NOT EXISTS workspace_invites (
    invite_id    TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    email        TEXT NOT NULL,
    role         TEXT NOT NULL DEFAULT 'member',
    token        TEXT NOT NULL,
    expires_at   TEXT NOT NULL,
    accepted_at  TEXT,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    CONSTRAINT workspace_invites_token_unique UNIQUE (token)
);

CREATE TABLE IF NOT EXISTS projects (
    project_id   TEXT PRIMARY KEY,
    workspace_id TEXT REFERENCES workspaces(workspace_id),
    name         TEXT NOT NULL,
    slug         TEXT NOT NULL,
    description  TEXT,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    archived_at  TEXT,
    CONSTRAINT projects_ws_slug_unique UNIQUE (workspace_id, slug)
);

CREATE TABLE IF NOT EXISTS project_members (
    project_id TEXT NOT NULL REFERENCES projects(project_id),
    user_id    TEXT NOT NULL REFERENCES users(user_id),
    role       TEXT NOT NULL DEFAULT 'contributor',
    joined_at  TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (project_id, user_id)
);

CREATE TABLE IF NOT EXISTS project_config (
    project_id TEXT NOT NULL REFERENCES projects(project_id),
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    PRIMARY KEY (project_id, key)
);

CREATE TABLE IF NOT EXISTS config (
    scope      TEXT NOT NULL,
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (scope, key)
);

CREATE TABLE IF NOT EXISTS api_tokens (
    token_id     TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    token_hash   TEXT NOT NULL,
    token_prefix TEXT NOT NULL,
    name         TEXT,
    scopes       TEXT,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    expires_at   TEXT,
    revoked_at   TEXT,
    last_used_at TEXT,
    CONSTRAINT api_tokens_hash_unique UNIQUE (token_hash)
);

CREATE TABLE IF NOT EXISTS roles (
    role_id     TEXT PRIMARY KEY,
    name        TEXT NOT NULL,
    description TEXT,
    is_system   INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    CONSTRAINT roles_name_unique UNIQUE (name)
);

CREATE TABLE IF NOT EXISTS permissions (
    permission_id TEXT PRIMARY KEY,
    name          TEXT NOT NULL,
    description   TEXT,
    CONSTRAINT permissions_name_unique UNIQUE (name)
);

CREATE TABLE IF NOT EXISTS role_permissions (
    role_id       TEXT NOT NULL REFERENCES roles(role_id),
    permission_id TEXT NOT NULL REFERENCES permissions(permission_id),
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE IF NOT EXISTS user_roles (
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    role_id      TEXT NOT NULL REFERENCES roles(role_id),
    workspace_id TEXT NOT NULL DEFAULT '',
    granted_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (user_id, role_id, workspace_id)
);

CREATE TABLE IF NOT EXISTS audit_governance (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    timestamp    TEXT NOT NULL,
    phase        TEXT NOT NULL,
    tool         TEXT NOT NULL,
    session_id   TEXT,
    workspace_id TEXT,
    project_id   TEXT,
    action       TEXT NOT NULL,
    rule         TEXT NOT NULL,
    reason       TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS audit_bash (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    timestamp    TEXT NOT NULL,
    command      TEXT NOT NULL,
    session_id   TEXT,
    workspace_id TEXT,
    project_id   TEXT,
    exit_code    INTEGER NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_api_tokens_user          ON api_tokens(user_id);
CREATE INDEX IF NOT EXISTS ix_api_tokens_hash          ON api_tokens(token_hash);
CREATE INDEX IF NOT EXISTS ix_audit_governance_session ON audit_governance(session_id);
CREATE INDEX IF NOT EXISTS ix_audit_bash_session       ON audit_bash(session_id);

-- ── V002 Sessions ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS sessions (
    session_id   TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL DEFAULT '',
    workspace_id TEXT,
    project_id   TEXT,
    model        TEXT,
    status       TEXT NOT NULL DEFAULT 'active',
    title        TEXT,
    mcp_servers  TEXT,
    started_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    ended_at     TEXT,
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE TABLE IF NOT EXISTS session_entries (
    entry_id      BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id    TEXT NOT NULL REFERENCES sessions(session_id) ON DELETE CASCADE,
    entry_uid     TEXT NOT NULL,
    timestamp     TEXT NOT NULL,
    role          TEXT NOT NULL,
    content       TEXT NOT NULL,
    model         TEXT,
    provider      TEXT,
    input_tokens  INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    tool_name     TEXT,
    tool_use_id   TEXT,
    is_error      INTEGER NOT NULL DEFAULT 0,
    -- Full-text search via PostgreSQL tsvector (replaces SQLite FTS5)
    search_vector tsvector GENERATED ALWAYS AS (to_tsvector('english', content)) STORED,
    CONSTRAINT uq_session_entries_uid UNIQUE (session_id, entry_uid)
);

CREATE TABLE IF NOT EXISTS token_usage (
    id            BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id    TEXT NOT NULL,
    user_id       TEXT NOT NULL DEFAULT '',
    workspace_id  TEXT,
    project_id    TEXT,
    model         TEXT NOT NULL,
    input_tokens  INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd      DOUBLE PRECISION,
    recorded_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS ix_sessions_user         ON sessions(user_id);
CREATE INDEX IF NOT EXISTS ix_sessions_status       ON sessions(status);
CREATE INDEX IF NOT EXISTS ix_sessions_workspace    ON sessions(workspace_id);
CREATE INDEX IF NOT EXISTS ix_sessions_project      ON sessions(project_id);
CREATE INDEX IF NOT EXISTS ix_session_entries_session ON session_entries(session_id);
CREATE INDEX IF NOT EXISTS ix_session_entries_fts   ON session_entries USING GIN(search_vector);
CREATE INDEX IF NOT EXISTS ix_token_usage_session   ON token_usage(session_id);
CREATE INDEX IF NOT EXISTS ix_token_usage_user      ON token_usage(user_id);
CREATE INDEX IF NOT EXISTS ix_token_usage_workspace ON token_usage(workspace_id);
CREATE INDEX IF NOT EXISTS ix_token_usage_project   ON token_usage(project_id);

-- ── V003 Memory ───────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS session_summaries (
    session_id           TEXT PRIMARY KEY,
    project              TEXT NOT NULL,
    workspace_id         TEXT,
    started_at           TEXT NOT NULL,
    ended_at             TEXT NOT NULL,
    tasks                TEXT NOT NULL DEFAULT '[]',
    tools_used           TEXT NOT NULL DEFAULT '[]',
    files_modified       TEXT NOT NULL DEFAULT '[]',
    outcome              TEXT NOT NULL DEFAULT 'Unknown',
    total_input_tokens   INTEGER NOT NULL DEFAULT 0,
    total_output_tokens  INTEGER NOT NULL DEFAULT 0,
    turn_count           INTEGER NOT NULL DEFAULT 0,
    error_count          INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS learned_patterns (
    id             TEXT PRIMARY KEY,
    pattern        TEXT NOT NULL,
    project        TEXT NOT NULL,
    source_session TEXT,
    confidence     DOUBLE PRECISION NOT NULL DEFAULT 0.5,
    created_at     TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    last_used      TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE TABLE IF NOT EXISTS instincts (
    id         TEXT PRIMARY KEY,
    trigger    TEXT NOT NULL,
    action     TEXT NOT NULL,
    confidence DOUBLE PRECISION NOT NULL DEFAULT 0.5,
    evidence   TEXT NOT NULL DEFAULT '[]',
    created_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS ix_session_summaries_project   ON session_summaries(project);
CREATE INDEX IF NOT EXISTS ix_session_summaries_workspace ON session_summaries(workspace_id);
CREATE INDEX IF NOT EXISTS ix_learned_patterns_project    ON learned_patterns(project);

-- ── V004 Credentials ──────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS credentials (
    key_hash     TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL DEFAULT '',
    workspace_id TEXT,
    nonce        BYTEA NOT NULL,
    tag          BYTEA NOT NULL,
    ciphertext   BYTEA NOT NULL,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

-- ── V005 Swarm + Evals ────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS swarm_events (
    id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    swarm_id       TEXT NOT NULL,
    event_type     TEXT NOT NULL,
    agent_id       TEXT,
    workspace_id   TEXT,
    project_id     TEXT,
    payload        TEXT NOT NULL DEFAULT '{}',
    timestamp      TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    kind           TEXT NOT NULL DEFAULT 'swarm',
    run_id         TEXT,
    user_id        TEXT,
    parent_swarm_id TEXT
);

CREATE TABLE IF NOT EXISTS eval_runs (
    run_id           TEXT PRIMARY KEY,
    suite_name       TEXT NOT NULL,
    workspace_id     TEXT,
    started_at       TEXT NOT NULL,
    duration_seconds DOUBLE PRECISION NOT NULL DEFAULT 0,
    pass_rate        DOUBLE PRECISION NOT NULL DEFAULT 0,
    pass_at_1_rate   DOUBLE PRECISION NOT NULL DEFAULT 0,
    total_passed     INTEGER NOT NULL DEFAULT 0,
    total_failed     INTEGER NOT NULL DEFAULT 0,
    total_skipped    INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS eval_results (
    id               BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    run_id           TEXT NOT NULL REFERENCES eval_runs(run_id) ON DELETE CASCADE,
    eval_name        TEXT NOT NULL,
    category         TEXT NOT NULL,
    grader_type      TEXT NOT NULL,
    passed           INTEGER NOT NULL DEFAULT 0,
    pass_at_1        INTEGER NOT NULL DEFAULT 0,
    pass_count       INTEGER NOT NULL DEFAULT 0,
    attempt_count    INTEGER NOT NULL DEFAULT 0,
    average_score    DOUBLE PRECISION,
    duration_seconds DOUBLE PRECISION NOT NULL DEFAULT 0,
    skipped          INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_swarm_events_swarm     ON swarm_events(swarm_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_workspace ON swarm_events(workspace_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_project   ON swarm_events(project_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_run_id    ON swarm_events(run_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_kind      ON swarm_events(kind);
CREATE INDEX IF NOT EXISTS ix_swarm_events_user      ON swarm_events(user_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_parent    ON swarm_events(parent_swarm_id);
CREATE INDEX IF NOT EXISTS ix_eval_runs_suite        ON eval_runs(suite_name);
CREATE INDEX IF NOT EXISTS ix_eval_runs_workspace    ON eval_runs(workspace_id);
CREATE INDEX IF NOT EXISTS ix_eval_results_run       ON eval_results(run_id);

-- ── V006 Workspace Memory ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS workspace_memory (
    memory_id    TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id) ON DELETE CASCADE,
    layer        TEXT NOT NULL,
    content      TEXT NOT NULL,
    confidence   DOUBLE PRECISION,
    project_id   TEXT,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS ix_workspace_memory_workspace ON workspace_memory(workspace_id);
CREATE INDEX IF NOT EXISTS ix_workspace_memory_layer     ON workspace_memory(workspace_id, layer);
CREATE INDEX IF NOT EXISTS ix_workspace_memory_project   ON workspace_memory(workspace_id, project_id);

-- ── V010 Runtime Traces ───────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS runtime_traces (
    id             BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    runtime_run_id TEXT NOT NULL,
    plan_id        TEXT NOT NULL,
    plan_version   INTEGER NOT NULL,
    step_index     INTEGER,
    entry_type     TEXT NOT NULL,
    payload        TEXT NOT NULL DEFAULT '{}',
    timestamp      TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    session_id     TEXT,
    workspace_id   TEXT,
    project_id     TEXT
);

CREATE TABLE IF NOT EXISTS mission_scratchpad (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mission_id   TEXT NOT NULL,
    step_index   INTEGER NOT NULL,
    agent_id     TEXT,
    namespace    TEXT NOT NULL DEFAULT 'default',
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,
    created_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    workspace_id TEXT,
    project_id   TEXT
);

CREATE INDEX IF NOT EXISTS ix_runtime_traces_run        ON runtime_traces(runtime_run_id, id);
CREATE INDEX IF NOT EXISTS ix_runtime_traces_entry_type ON runtime_traces(entry_type);
CREATE INDEX IF NOT EXISTS ix_runtime_traces_workspace  ON runtime_traces(workspace_id);
CREATE INDEX IF NOT EXISTS ix_runtime_traces_project    ON runtime_traces(project_id);
CREATE INDEX IF NOT EXISTS ix_mission_scratchpad_mission   ON mission_scratchpad(mission_id, step_index);
CREATE INDEX IF NOT EXISTS ix_mission_scratchpad_workspace ON mission_scratchpad(workspace_id);

-- ── V011 Missions ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS missions (
    id            TEXT PRIMARY KEY,
    goal          TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'planning',
    plan_json     TEXT NOT NULL DEFAULT '{}',
    created_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    completed_at  TEXT,
    session_id    TEXT,
    workspace_id  TEXT,
    project_id    TEXT,
    owner_user_id TEXT
);

CREATE TABLE IF NOT EXISTS mission_events (
    id           BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    mission_id   TEXT NOT NULL,
    event_type   TEXT NOT NULL,
    payload      TEXT NOT NULL DEFAULT '{}',
    timestamp    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    workspace_id TEXT,
    project_id   TEXT
);

CREATE INDEX IF NOT EXISTS ix_missions_status    ON missions(status);
CREATE INDEX IF NOT EXISTS ix_missions_workspace ON missions(workspace_id);
CREATE INDEX IF NOT EXISTS ix_missions_project   ON missions(project_id);
CREATE INDEX IF NOT EXISTS ix_missions_owner     ON missions(owner_user_id);
CREATE INDEX IF NOT EXISTS ix_mission_events_mission   ON mission_events(mission_id, id);
CREATE INDEX IF NOT EXISTS ix_mission_events_workspace ON mission_events(workspace_id);

-- ── V012 Unified Orchestration ────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS teams (
    team_id               TEXT PRIMARY KEY,
    workspace_id          TEXT NOT NULL,
    project_id            TEXT,
    name                  TEXT NOT NULL,
    description           TEXT,
    origin                TEXT NOT NULL DEFAULT 'user',
    created_by            TEXT NOT NULL,
    created_at            TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    run_mode              TEXT NOT NULL DEFAULT 'sequential',
    max_concurrent        INTEGER NOT NULL DEFAULT 1,
    file_locks_enabled    INTEGER NOT NULL DEFAULT 0,
    quality_gate_enabled  INTEGER NOT NULL DEFAULT 0,
    quality_gate_threshold INTEGER NOT NULL DEFAULT 7,
    decomposition_mode    TEXT NOT NULL DEFAULT 'off'
);

CREATE TABLE IF NOT EXISTS team_members (
    member_id     TEXT PRIMARY KEY,
    team_id       TEXT NOT NULL REFERENCES teams(team_id) ON DELETE CASCADE,
    workspace_id  TEXT NOT NULL,
    project_id    TEXT,
    name          TEXT NOT NULL,
    role          TEXT NOT NULL DEFAULT 'general',
    template      TEXT,
    system_prompt TEXT,
    allowed_tools TEXT,
    model_level   TEXT,
    created_by    TEXT NOT NULL,
    created_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    last_used_at  TEXT,
    status        TEXT NOT NULL DEFAULT 'active'
);

CREATE TABLE IF NOT EXISTS agent_runs (
    run_id        TEXT PRIMARY KEY,
    parent_run_id TEXT,
    team_id       TEXT,
    member_id     TEXT,
    workspace_id  TEXT NOT NULL,
    project_id    TEXT,
    user_id       TEXT NOT NULL,
    kind          TEXT NOT NULL,
    status        TEXT NOT NULL DEFAULT 'queued',
    started_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    ended_at      TEXT,
    input_tokens  INTEGER NOT NULL DEFAULT 0,
    output_tokens INTEGER NOT NULL DEFAULT 0,
    cost_usd      DOUBLE PRECISION,
    prompt        TEXT
);

CREATE INDEX IF NOT EXISTS ix_teams_workspace      ON teams(workspace_id);
CREATE INDEX IF NOT EXISTS ix_teams_project        ON teams(project_id);
CREATE INDEX IF NOT EXISTS ix_teams_name           ON teams(workspace_id, name);
CREATE INDEX IF NOT EXISTS ix_team_members_team      ON team_members(team_id);
CREATE INDEX IF NOT EXISTS ix_team_members_workspace ON team_members(workspace_id);
CREATE INDEX IF NOT EXISTS ix_agent_runs_parent    ON agent_runs(parent_run_id);
CREATE INDEX IF NOT EXISTS ix_agent_runs_team      ON agent_runs(team_id);
CREATE INDEX IF NOT EXISTS ix_agent_runs_workspace ON agent_runs(workspace_id);
CREATE INDEX IF NOT EXISTS ix_agent_runs_user      ON agent_runs(user_id);
CREATE INDEX IF NOT EXISTS ix_agent_runs_kind      ON agent_runs(kind);
CREATE INDEX IF NOT EXISTS ix_agent_runs_status    ON agent_runs(status);

-- ── V013 Coordination Mailbox ─────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS coordination_events (
    id                BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    source_group_id   TEXT NOT NULL,
    target_group_id   TEXT NOT NULL,
    event_type        TEXT NOT NULL,
    payload           TEXT NOT NULL DEFAULT '{}',
    status            TEXT NOT NULL DEFAULT 'pending',
    workspace_id      TEXT NOT NULL,
    project_id        TEXT,
    created_at        TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    delivered_at      TEXT,
    acknowledged_at   TEXT
);

CREATE TABLE IF NOT EXISTS group_pm_assignments (
    group_id      TEXT PRIMARY KEY,
    group_type    TEXT NOT NULL,
    pm_template   TEXT NOT NULL DEFAULT 'pm-coordinator',
    workspace_id  TEXT NOT NULL,
    project_id    TEXT,
    created_at    TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS ix_coordination_events_target ON coordination_events(target_group_id, status);
CREATE INDEX IF NOT EXISTS ix_coordination_events_source ON coordination_events(source_group_id);
CREATE INDEX IF NOT EXISTS ix_coordination_events_ws     ON coordination_events(workspace_id);
CREATE INDEX IF NOT EXISTS ix_group_pm_assignments_ws    ON group_pm_assignments(workspace_id);

-- ── V017 Hooks ────────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS hooks (
    hook_id      TEXT PRIMARY KEY,
    event        TEXT    NOT NULL,
    command      TEXT    NOT NULL,
    matcher_tool TEXT,
    matcher_glob TEXT,
    timeout_ms   INTEGER NOT NULL DEFAULT 30000,
    enabled      INTEGER NOT NULL DEFAULT 1,
    created_at   TEXT    NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at   TEXT    NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS hooks_event_enabled_idx ON hooks(event) WHERE enabled = 1;

-- ── V018 Workspace Settings ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS workspace_settings (
    workspace_id TEXT NOT NULL DEFAULT '',
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,
    updated_at   TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (workspace_id, key)
);

CREATE INDEX IF NOT EXISTS workspace_settings_key_idx ON workspace_settings(key);

-- ── V019 MCP/LSP Servers ──────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS mcp_servers (
    id                TEXT NOT NULL DEFAULT '',
    name              TEXT PRIMARY KEY,
    command           TEXT NOT NULL DEFAULT '',
    args_json         TEXT NOT NULL DEFAULT '[]',
    env_json          TEXT NOT NULL DEFAULT '{}',
    oauth_config_json TEXT,
    url               TEXT,
    headers_json      TEXT NOT NULL DEFAULT '{}',
    created_at        TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at        TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_servers_id ON mcp_servers(id) WHERE id <> '';

CREATE TABLE IF NOT EXISTS lsp_servers (
    language   TEXT PRIMARY KEY,
    command    TEXT NOT NULL DEFAULT '',
    args_json  TEXT NOT NULL DEFAULT '[]',
    env_json   TEXT NOT NULL DEFAULT '{}',
    created_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

-- ── V020 User Preferences ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS user_preferences (
    user_id    TEXT NOT NULL,
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    PRIMARY KEY (user_id, key)
);

CREATE INDEX IF NOT EXISTS user_preferences_key_idx ON user_preferences(key);

-- ── V021 Provider Profiles ────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS provider_profiles (
    profile_id     TEXT PRIMARY KEY,
    user_id        TEXT NOT NULL,
    name           TEXT NOT NULL,
    provider_kind  TEXT NOT NULL,
    base_url       TEXT NOT NULL,
    default_model  TEXT,
    max_tokens     INTEGER,
    credential_id  TEXT NOT NULL,
    workspace_id   TEXT,
    created_at     TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at     TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS provider_profiles_user_idx      ON provider_profiles(user_id);
CREATE INDEX IF NOT EXISTS provider_profiles_workspace_idx ON provider_profiles(workspace_id)
    WHERE workspace_id IS NOT NULL;

-- ── V026 Auth Credentials ─────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS server_settings (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE TABLE IF NOT EXISTS password_reset_tokens (
    token_id   TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    expires_at TEXT NOT NULL,
    used_at    TEXT,
    CONSTRAINT prt_hash_unique UNIQUE (token_hash)
);

CREATE INDEX IF NOT EXISTS ix_prt_user ON password_reset_tokens(user_id);
CREATE INDEX IF NOT EXISTS ix_prt_hash ON password_reset_tokens(token_hash);

-- ── V030 Activity is Private ──────────────────────────────────────────────────

ALTER TABLE missions   ADD COLUMN IF NOT EXISTS is_private INTEGER NOT NULL DEFAULT 0;
ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS is_private INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sessions   ADD COLUMN IF NOT EXISTS is_private INTEGER NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS ix_missions_is_private   ON missions(is_private);
CREATE INDEX IF NOT EXISTS ix_agent_runs_is_private ON agent_runs(is_private);
CREATE INDEX IF NOT EXISTS ix_sessions_is_private   ON sessions(is_private);

-- ── V031 Session Agent Name ───────────────────────────────────────────────────

ALTER TABLE sessions ADD COLUMN IF NOT EXISTS agent_name TEXT;

CREATE INDEX IF NOT EXISTS idx_sessions_agent_name ON sessions(agent_name) WHERE agent_name IS NOT NULL;

-- ── V032 MCP Trust Rules ──────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS mcp_trust_rules (
    rule_id      TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    server_name  TEXT NOT NULL,
    tool_pattern TEXT NOT NULL,
    action       TEXT NOT NULL,
    reason       TEXT,
    created_at   TEXT NOT NULL,
    updated_at   TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_mcp_trust_rules_workspace ON mcp_trust_rules(workspace_id);
CREATE INDEX IF NOT EXISTS idx_mcp_trust_rules_server    ON mcp_trust_rules(server_name);

-- ── V033/V034/V036 Knowledge Pages ───────────────────────────────────────────

CREATE TABLE IF NOT EXISTS knowledge_pages (
    knowledge_id      TEXT NOT NULL PRIMARY KEY,
    kind              TEXT NOT NULL,
    slug              TEXT NOT NULL,
    name              TEXT NOT NULL,
    description       TEXT NOT NULL DEFAULT '',
    tier              TEXT NOT NULL DEFAULT 'User',
    trigger           TEXT,
    agents            TEXT,
    tools             TEXT,
    industry          TEXT,
    default_format    TEXT,
    category          TEXT,
    role              TEXT,
    recommended_level TEXT,
    fields_json       TEXT,
    filename_template TEXT,
    body              TEXT NOT NULL DEFAULT '',
    workspace_id      TEXT NOT NULL DEFAULT '',
    created_at        TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at        TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    UNIQUE (kind, slug, workspace_id)
);

CREATE INDEX IF NOT EXISTS idx_knowledge_pages_kind      ON knowledge_pages(kind);
CREATE INDEX IF NOT EXISTS idx_knowledge_pages_workspace ON knowledge_pages(workspace_id);

ALTER TABLE knowledge_pages ADD COLUMN IF NOT EXISTS role              TEXT;
ALTER TABLE knowledge_pages ADD COLUMN IF NOT EXISTS recommended_level TEXT;
ALTER TABLE knowledge_pages ADD COLUMN IF NOT EXISTS fields_json       TEXT;
ALTER TABLE knowledge_pages ADD COLUMN IF NOT EXISTS filename_template TEXT;

-- ── V038 Knowledge Attributions ───────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS knowledge_attributions (
    id          BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    session_id  TEXT    NOT NULL,
    turn_index  INTEGER NOT NULL,
    kind        TEXT    NOT NULL,
    slug        TEXT    NOT NULL,
    used_at     TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_knowledge_attributions_session ON knowledge_attributions(session_id);

-- ── V039 Keystore ─────────────────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS keystore (
    scope      TEXT NOT NULL PRIMARY KEY,
    key_hex    TEXT NOT NULL,
    created_at TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

-- ── V040 MCP Server stable ID ────────────────────────────────────────────────

ALTER TABLE mcp_servers ADD COLUMN IF NOT EXISTS id TEXT NOT NULL DEFAULT '';
CREATE UNIQUE INDEX IF NOT EXISTS idx_mcp_servers_id ON mcp_servers(id) WHERE id <> '';
UPDATE mcp_servers SET id = gen_random_uuid()::text WHERE id = '';

-- ── V041 Workspace Memory Privacy ────────────────────────────────────────────

ALTER TABLE workspace_memory ADD COLUMN IF NOT EXISTS owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE workspace_memory ADD COLUMN IF NOT EXISTS is_private    INTEGER NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS ix_workspace_memory_owner ON workspace_memory(owner_user_id);

-- ── V042 Memory Owner User ID ─────────────────────────────────────────────────

ALTER TABLE session_summaries ADD COLUMN IF NOT EXISTS owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE learned_patterns  ADD COLUMN IF NOT EXISTS owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE instincts          ADD COLUMN IF NOT EXISTS owner_user_id TEXT NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS ix_session_summaries_owner ON session_summaries(owner_user_id);
CREATE INDEX IF NOT EXISTS ix_learned_patterns_owner  ON learned_patterns(owner_user_id);
CREATE INDEX IF NOT EXISTS ix_instincts_owner         ON instincts(owner_user_id);

-- ── V043 Drop username column ─────────────────────────────────────────────────
-- Fresh installs never have this column. IF EXISTS makes this safe to run on both
-- new and upgraded instances. user_id remains the GoTrue UUID in Supabase mode.

ALTER TABLE public.users DROP COLUMN IF EXISTS username;

-- ── Schema version tracking ───────────────────────────────────────────────────

CREATE TABLE IF NOT EXISTS sovrant_schema_version (
    id          INTEGER PRIMARY KEY DEFAULT 1,
    version     INTEGER NOT NULL,
    applied_at  TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    CONSTRAINT single_row CHECK (id = 1)
);

INSERT INTO sovrant_schema_version (id, version)
VALUES (1, 43)
ON CONFLICT (id) DO UPDATE SET version = EXCLUDED.version, applied_at = EXCLUDED.applied_at;

-- ════════════════════════════════════════════════════════════════════════════════
-- SUPABASE AUTH EXTENSION
-- Keeps public.users in sync with auth.users (GoTrue) automatically.
-- Every time a user signs up or updates their email/role in Supabase Auth,
-- these triggers mirror the change so all FK constraints remain intact.
--
-- user_id = auth.users.id::TEXT (UUID string, NOT the email address).
-- The email-as-PK rewrite in V043 applies to SQLite standalone only.
-- ════════════════════════════════════════════════════════════════════════════════

-- ── Mirror trigger: auth.users INSERT → public.users INSERT ──────────────────

CREATE OR REPLACE FUNCTION public.handle_auth_user_created()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    _role TEXT;
BEGIN
    -- Read sovrant_role from app_metadata (service-role only, user cannot self-set).
    -- Whitelist: only 'admin' is elevated; anything else (missing, null, unknown) → 'user'.
    _role := CASE
        WHEN NEW.raw_app_meta_data->>'sovrant_role' = 'admin' THEN 'admin'
        ELSE 'user'
    END;

    INSERT INTO public.users (user_id, email, role, status, created_at, updated_at)
    VALUES (
        NEW.id::TEXT,
        NEW.email,
        _role,
        'active',
        to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'),
        to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
    )
    ON CONFLICT (user_id) DO NOTHING;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER on_auth_user_created
    AFTER INSERT ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_auth_user_created();

-- ── Mirror trigger: auth.users email/role UPDATE → public.users UPDATE ───────
-- Fires on email changes AND raw_app_meta_data changes so that setting
-- sovrant_role in app_metadata immediately syncs to public.users.role.

CREATE OR REPLACE FUNCTION public.handle_auth_user_updated()
RETURNS TRIGGER LANGUAGE plpgsql SECURITY DEFINER SET search_path = public AS $$
DECLARE
    _role TEXT;
BEGIN
    IF NEW.raw_app_meta_data IS DISTINCT FROM OLD.raw_app_meta_data THEN
        _role := CASE
            WHEN NEW.raw_app_meta_data->>'sovrant_role' = 'admin' THEN 'admin'
            ELSE 'user'
        END;
        UPDATE public.users
        SET    email      = NEW.email,
               role       = _role,
               updated_at = to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
        WHERE  user_id = NEW.id::TEXT;
    ELSE
        UPDATE public.users
        SET    email      = NEW.email,
               updated_at = to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')
        WHERE  user_id = NEW.id::TEXT;
    END IF;
    RETURN NEW;
END;
$$;

CREATE OR REPLACE TRIGGER on_auth_user_updated
    AFTER UPDATE OF email, raw_app_meta_data ON auth.users
    FOR EACH ROW EXECUTE FUNCTION public.handle_auth_user_updated();

-- ── Notes for Supabase deployments ───────────────────────────────────────────
-- • public.users.password_hash is always NULL for Supabase-authenticated users.
--   GoTrue owns password management; the column is inert but kept for schema
--   parity with SQLite / standalone Postgres deployments.
-- • password_reset_tokens is also inert — Supabase handles password resets.
--   The table is retained so the schema remains identical across all modes.
-- • svt_* api_tokens still work for CLI / headless API access in Supabase mode.
--   Only interactive UI sessions use Supabase JWTs; machine tokens use the table.
-- • First admin: create the user in the Supabase dashboard (Auth → Users).
--   The mirror trigger fires automatically (role defaults to 'user'). Elevate to
--   admin by setting sovrant_role in app_metadata (service-role only):
--     UPDATE auth.users
--     SET    raw_app_meta_data = jsonb_set(
--                COALESCE(raw_app_meta_data, '{}'), '{sovrant_role}', '"admin"')
--     WHERE  email = 'you@example.com';
--   The on_auth_user_updated trigger fires and syncs role = 'admin' to
--   public.users automatically. Never UPDATE public.users.role directly.

-- ── Row-Level Security (recommended for Supabase deployments) ────────────────
-- Uncomment to enforce the owner_user_id privacy model at the database layer.
-- Required if any Supabase Edge Functions or dashboard queries need to respect
-- per-user memory privacy without going through the Sovrant API.
--
-- NOTE: auth.uid() only works for Supabase JWT holders. svt_* machine tokens
-- use the service role and bypass RLS entirely by design. Sovrant's own API
-- enforces privacy at the application layer for machine-token callers.

-- ALTER TABLE workspace_memory  ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE session_summaries ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE learned_patterns  ENABLE ROW LEVEL SECURITY;
-- ALTER TABLE instincts         ENABLE ROW LEVEL SECURITY;

-- CREATE POLICY workspace_memory_owner ON workspace_memory
--     USING (is_private = 0 OR owner_user_id = auth.uid()::text);

-- CREATE POLICY session_summaries_owner ON session_summaries
--     USING (owner_user_id = '' OR owner_user_id = auth.uid()::text);

-- CREATE POLICY learned_patterns_owner ON learned_patterns
--     USING (owner_user_id = '' OR owner_user_id = auth.uid()::text);

-- CREATE POLICY instincts_owner ON instincts
--     USING (owner_user_id = '' OR owner_user_id = auth.uid()::text);
