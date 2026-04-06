-- V001: Foundation tables — users, workspaces, projects, config, auth, RBAC
-- Phase 32 core + Phase 33-37 placeholders (created empty, populated later)

-- ── Users (Phase 35-ready) ──────────────────────────────────────────────────
CREATE TABLE users (
    user_id    TEXT PRIMARY KEY,
    username   TEXT NOT NULL UNIQUE,
    email      TEXT UNIQUE,
    role       TEXT NOT NULL DEFAULT 'user',
    team       TEXT,
    status     TEXT NOT NULL DEFAULT 'active',
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

-- ── Workspaces (Phase 33 placeholder) ───────────────────────────────────────
CREATE TABLE workspaces (
    workspace_id TEXT PRIMARY KEY,
    type         TEXT NOT NULL DEFAULT 'personal',
    name         TEXT NOT NULL,
    slug         TEXT NOT NULL UNIQUE,
    owner_id     TEXT NOT NULL REFERENCES users(user_id),
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE workspace_members (
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    role         TEXT NOT NULL DEFAULT 'member',
    joined_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (workspace_id, user_id)
);

CREATE TABLE workspace_config (
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    key          TEXT NOT NULL,
    value        TEXT NOT NULL,
    PRIMARY KEY (workspace_id, key)
);

CREATE TABLE workspace_invites (
    invite_id    TEXT PRIMARY KEY,
    workspace_id TEXT NOT NULL REFERENCES workspaces(workspace_id),
    email        TEXT NOT NULL,
    role         TEXT NOT NULL DEFAULT 'member',
    token        TEXT NOT NULL UNIQUE,
    expires_at   TEXT NOT NULL,
    accepted_at  TEXT,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

-- ── Projects (Phase 34 placeholder) ─────────────────────────────────────────
CREATE TABLE projects (
    project_id   TEXT PRIMARY KEY,
    workspace_id TEXT REFERENCES workspaces(workspace_id),
    name         TEXT NOT NULL,
    slug         TEXT NOT NULL,
    description  TEXT,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    archived_at  TEXT,
    UNIQUE (workspace_id, slug)
);

CREATE TABLE project_members (
    project_id TEXT NOT NULL REFERENCES projects(project_id),
    user_id    TEXT NOT NULL REFERENCES users(user_id),
    role       TEXT NOT NULL DEFAULT 'contributor',
    joined_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (project_id, user_id)
);

CREATE TABLE project_config (
    project_id TEXT NOT NULL REFERENCES projects(project_id),
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    PRIMARY KEY (project_id, key)
);

-- ── Config (scoped key-value) ───────────────────────────────────────────────
CREATE TABLE config (
    scope      TEXT NOT NULL,
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (scope, key)
);

-- ── API Tokens (Phase 35 placeholder) ───────────────────────────────────────
CREATE TABLE api_tokens (
    token_id     TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    token_hash   TEXT NOT NULL UNIQUE,
    token_prefix TEXT NOT NULL,
    name         TEXT,
    scopes       TEXT,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    expires_at   TEXT,
    revoked_at   TEXT
);

CREATE INDEX ix_api_tokens_user ON api_tokens(user_id);
CREATE INDEX ix_api_tokens_hash ON api_tokens(token_hash);

-- ── RBAC (Phase 37 placeholder) ─────────────────────────────────────────────
CREATE TABLE roles (
    role_id     TEXT PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    description TEXT,
    is_system   INTEGER NOT NULL DEFAULT 0,
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE permissions (
    permission_id TEXT PRIMARY KEY,
    name          TEXT NOT NULL UNIQUE,
    description   TEXT
);

CREATE TABLE role_permissions (
    role_id       TEXT NOT NULL REFERENCES roles(role_id),
    permission_id TEXT NOT NULL REFERENCES permissions(permission_id),
    PRIMARY KEY (role_id, permission_id)
);

CREATE TABLE user_roles (
    user_id      TEXT NOT NULL REFERENCES users(user_id),
    role_id      TEXT NOT NULL REFERENCES roles(role_id),
    workspace_id TEXT NOT NULL DEFAULT '',
    granted_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (user_id, role_id, workspace_id)
);

-- ── Audit (Phase 32 core) ──────────────────────────────────────────────────
CREATE TABLE audit_governance (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
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

CREATE TABLE audit_bash (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp    TEXT NOT NULL,
    command      TEXT NOT NULL,
    session_id   TEXT,
    workspace_id TEXT,
    project_id   TEXT,
    exit_code    INTEGER NOT NULL
);

CREATE INDEX ix_audit_governance_session ON audit_governance(session_id);
CREATE INDEX ix_audit_bash_session ON audit_bash(session_id);
