-- V022: Phase 87 Track D — Workspace Identity Unification.
--
-- Pre-Phase-87 callers wrote artifacts and DB rows under the bare workspace
-- sentinel "personal" (the old ArtifactScope.DefaultWorkspaceId). The
-- canonical form, minted by SqliteWorkspaceStore.CreatePersonalWorkspaceAsync,
-- is "ws-personal-{userId}". Any row that still references "personal" is a
-- straggler from the unification gap.
--
-- This migration rewrites those stragglers in-place. It is idempotent: if no
-- "personal" rows exist, every UPDATE is a no-op. Filesystem sweep of
-- ~/.sovrant/artifacts/personal/ is handled by the C# WorkspaceIdentityMigrator
-- at startup — SQL cannot move directories.
--
-- Note: PRAGMA foreign_keys is intentionally NOT toggled here. MigrationRunner
-- wraps each migration in a transaction, and pragmas issued inside a
-- transaction are silently ignored by SQLite. The UPDATEs below are ordered
-- so that the workspaces row is renamed first (it has no inbound FKs from
-- "personal") and child rows then resolve their canonical workspace_id via
-- the workspaces table, never producing a temporary FK violation.

-- ── 1. workspaces row itself ──────────────────────────────────────────────────
-- If a row literally named "personal" exists (created before V006 introduced
-- canonical IDs), rename it to ws-personal-{owner_id}. Skip if the canonical
-- target already exists — leave the legacy row in place rather than corrupt
-- a real workspace.
UPDATE workspaces
SET workspace_id = 'ws-personal-' || owner_id
WHERE workspace_id = 'personal'
  AND owner_id IS NOT NULL AND owner_id <> ''
  AND NOT EXISTS (
      SELECT 1 FROM workspaces w2
      WHERE w2.workspace_id = 'ws-personal-' || workspaces.owner_id
  );

-- ── 2. Child tables — rewrite "personal" → canonical ─────────────────────────
-- For tables with a direct user_id, derive the canonical id from that.
-- For tables without one, look up the (now-renamed) personal workspace by
-- type='personal'. If we can't resolve a target, leave the row alone — it's
-- safer to keep an orphan visible than silently misroute it.

UPDATE workspace_members
SET workspace_id = 'ws-personal-' || user_id
WHERE workspace_id = 'personal'
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.workspace_id = 'ws-personal-' || workspace_members.user_id
  );

UPDATE sessions
SET workspace_id = 'ws-personal-' || user_id
WHERE workspace_id = 'personal'
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.workspace_id = 'ws-personal-' || sessions.user_id
  );

UPDATE token_usage
SET workspace_id = 'ws-personal-' || user_id
WHERE workspace_id = 'personal'
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.workspace_id = 'ws-personal-' || token_usage.user_id
  );

UPDATE credentials
SET workspace_id = 'ws-personal-' || user_id
WHERE workspace_id = 'personal'
  AND user_id IS NOT NULL AND user_id <> ''
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.workspace_id = 'ws-personal-' || credentials.user_id
  );

-- projects has no created_by column — owner is indirect via the workspace.
-- For legacy data there was a single "personal" workspaces row (now renamed
-- to ws-personal-{owner_id}); reattach orphan projects to it. Multi-personal-
-- workspace databases never reached this state in practice.
UPDATE projects
SET workspace_id = (
    SELECT w.workspace_id FROM workspaces w
    WHERE w.type = 'personal'
      AND w.workspace_id LIKE 'ws-personal-%'
    ORDER BY w.created_at ASC
    LIMIT 1
)
WHERE workspace_id = 'personal'
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.type = 'personal'
        AND w.workspace_id LIKE 'ws-personal-%'
  );

-- workspace_settings has no user_id; resolve the canonical replacement via
-- the (already-renamed) personal workspaces row.
UPDATE workspace_settings
SET workspace_id = (
    SELECT w.workspace_id FROM workspaces w
    WHERE w.type = 'personal'
      AND w.workspace_id LIKE 'ws-personal-%'
    ORDER BY w.created_at ASC
    LIMIT 1
)
WHERE workspace_id = 'personal'
  AND EXISTS (
      SELECT 1 FROM workspaces w
      WHERE w.type = 'personal'
        AND w.workspace_id LIKE 'ws-personal-%'
  );

-- Indirect (session-linked) tables — pick up the new sessions.workspace_id.
UPDATE audit_governance
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = audit_governance.session_id
)
WHERE workspace_id = 'personal'
  AND session_id IS NOT NULL AND session_id <> ''
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = audit_governance.session_id
        AND s.workspace_id LIKE 'ws-personal-%'
  );

UPDATE audit_bash
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = audit_bash.session_id
)
WHERE workspace_id = 'personal'
  AND session_id IS NOT NULL AND session_id <> ''
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = audit_bash.session_id
        AND s.workspace_id LIKE 'ws-personal-%'
  );

UPDATE session_summaries
SET workspace_id = (
    SELECT s.workspace_id FROM sessions s
    WHERE s.session_id = session_summaries.session_id
)
WHERE workspace_id = 'personal'
  AND EXISTS (
      SELECT 1 FROM sessions s
      WHERE s.session_id = session_summaries.session_id
        AND s.workspace_id LIKE 'ws-personal-%'
  );
