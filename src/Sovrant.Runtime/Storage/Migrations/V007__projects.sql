-- V007: Project activation — indexes for project-scoped queries

-- ── Project indexes on existing tables ─────────────────────────────────────
CREATE INDEX IF NOT EXISTS ix_sessions_project       ON sessions(project_id);
CREATE INDEX IF NOT EXISTS ix_token_usage_project     ON token_usage(project_id);
CREATE INDEX IF NOT EXISTS ix_audit_governance_project ON audit_governance(project_id);
CREATE INDEX IF NOT EXISTS ix_audit_bash_project       ON audit_bash(project_id);
CREATE INDEX IF NOT EXISTS ix_swarm_events_project     ON swarm_events(project_id);

-- ── Project table indexes ──────────────────────────────────────────────────
CREATE INDEX IF NOT EXISTS ix_projects_workspace ON projects(workspace_id);
CREATE INDEX IF NOT EXISTS ix_project_members_user ON project_members(user_id);

-- ── Workspace memory project index (for project-scoped memory queries) ────
CREATE INDEX IF NOT EXISTS ix_workspace_memory_project ON workspace_memory(workspace_id, project_id);
