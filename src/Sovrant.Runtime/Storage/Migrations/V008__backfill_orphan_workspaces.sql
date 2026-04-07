-- V008: Backfill workspace_id on rows written before V006 introduced workspace scoping.
--
-- Pre-V006 databases hold sessions, token_usage, audit, credentials, and summaries
-- with workspace_id IS NULL or '' because the workspace concept did not exist yet.
-- After V006 creates ws-personal-{user_id} for each seeded user, those rows are
-- still orphaned from workspace-scoped views.
--
-- This migration is intentionally conservative: it only fills empty workspace_id,
-- and only when the matching ws-personal-{user_id} workspace already exists.
-- Rows for users without a personal workspace are left untouched.
--
-- Tables that have a `user_id` column are backfilled directly. Tables that only
-- have `session_id` (audit_*, session_summaries) are backfilled from sessions
-- *after* sessions itself has been backfilled, so the join sees the new value.

-- ── 1. Direct backfill: rows that carry user_id ────────────────────────────────

UPDATE sessions
SET workspace_id = 'ws-personal-' || user_id
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces
      WHERE workspace_id = 'ws-personal-' || sessions.user_id
  );

UPDATE token_usage
SET workspace_id = 'ws-personal-' || user_id
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces
      WHERE workspace_id = 'ws-personal-' || token_usage.user_id
  );

UPDATE credentials
SET workspace_id = 'ws-personal-' || user_id
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces
      WHERE workspace_id = 'ws-personal-' || credentials.user_id
  );

-- ── 2. Indirect backfill: rows that link via session_id only ───────────────────
-- These run after step 1 so the sessions.workspace_id values are already populated.

UPDATE audit_governance
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = audit_governance.session_id
)
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND session_id IS NOT NULL AND session_id <> ''
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = audit_governance.session_id
        AND s.workspace_id IS NOT NULL
        AND s.workspace_id <> ''
  );

UPDATE audit_bash
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = audit_bash.session_id
)
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND session_id IS NOT NULL AND session_id <> ''
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = audit_bash.session_id
        AND s.workspace_id IS NOT NULL
        AND s.workspace_id <> ''
  );

UPDATE session_summaries
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = session_summaries.session_id
)
WHERE (workspace_id IS NULL OR workspace_id = '')
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = session_summaries.session_id
        AND s.workspace_id IS NOT NULL
        AND s.workspace_id <> ''
  );

-- swarm_events and eval_runs have no user_id and no session_id linkage in V005,
-- so they cannot be safely backfilled by this migration. They will be handled by
-- the swarm-into-DB phase (see roadmap Phase 38a).
