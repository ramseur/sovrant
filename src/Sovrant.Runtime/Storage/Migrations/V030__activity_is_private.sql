-- V030: Phase 98 — User Dashboard — adds is_private flag to user-owned
-- activity records (missions, agent runs, sessions). Default 0 (public).
--
-- The User Dashboard surfaces a per-user cross-workspace activity view.
-- Each user sees their own records (public and private) plus other users'
-- public records in shared workspaces. Other users' private records are
-- excluded entirely. Command Center remains admin-only and unchanged.

ALTER TABLE missions   ADD COLUMN is_private INTEGER NOT NULL DEFAULT 0;
ALTER TABLE agent_runs ADD COLUMN is_private INTEGER NOT NULL DEFAULT 0;
ALTER TABLE sessions   ADD COLUMN is_private INTEGER NOT NULL DEFAULT 0;

CREATE INDEX ix_missions_is_private   ON missions(is_private);
CREATE INDEX ix_agent_runs_is_private ON agent_runs(is_private);
CREATE INDEX ix_sessions_is_private   ON sessions(is_private);
