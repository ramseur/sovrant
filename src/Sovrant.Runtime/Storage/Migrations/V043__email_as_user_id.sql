-- sovrant:no-fk
-- V043: Replace usr_{hex} primary keys with email addresses.
--
-- Every user registered through the auth flow has an email and a random
-- usr_{hex} user_id. The hex ID is opaque in FK columns — email is the
-- natural, already-unique, already-required identifier. This migration:
--   1. Builds old→new mappings in temp tables
--   2. Cascade-updates all FK and soft-reference columns
--   3. Updates the PKs (workspaces.workspace_id, users.user_id)
--   4. Recreates the users table without the now-redundant username column
--
-- Requires PRAGMA foreign_keys = OFF (handled by the -- sovrant:no-fk
-- directive; the MigrationRunner executes it before BeginTransaction).
-- Users without email (OS-seeded dev identities) are skipped — their
-- usr_{hex} IDs remain valid.

-- ── 1. Build ID mappings ──────────────────────────────────────────────────────

CREATE TEMP TABLE _uid_map AS
SELECT user_id AS old_uid, email AS new_uid
FROM users
WHERE user_id LIKE 'usr_%'
  AND email IS NOT NULL AND email != '';

-- Personal workspace IDs follow WorkspaceIdentity.DefaultPersonalFor:
--   non-alphanum/dash/underscore/dot → '-', prefix 'ws-personal-'
-- For real-world emails the only character that needs replacing is '@'.
CREATE TEMP TABLE _wid_map AS
SELECT
    w.workspace_id AS old_wid,
    'ws-personal-' || REPLACE(REPLACE(u.new_uid, '@', '-'), '+', '-') AS new_wid
FROM workspaces w
JOIN _uid_map u ON w.owner_id = u.old_uid
WHERE w.workspace_id LIKE 'ws-personal-usr_%'
  AND w.type = 'personal';

-- ── 2a. Update workspace_id FK children ──────────────────────────────────────

UPDATE workspace_members
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE workspace_config
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE workspace_invites
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE projects
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE workspace_memory
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

-- ── 2b. Update user_id FK children ───────────────────────────────────────────

UPDATE workspace_members
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE project_members
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE api_tokens
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE user_roles
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

-- ── 2c. Update workspaces.owner_id (FK to users) ─────────────────────────────

UPDATE workspaces
   SET owner_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_id)
 WHERE owner_id IN (SELECT old_uid FROM _uid_map);

-- ── 3. Update PKs ─────────────────────────────────────────────────────────────

UPDATE workspaces
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE users
   SET user_id = email
 WHERE user_id LIKE 'usr_%' AND email IS NOT NULL AND email != '';

-- ── 2d. Update soft-reference columns (no FK, data consistency) ───────────────

UPDATE sessions
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE sessions
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE token_usage
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE token_usage
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE credentials
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE credentials
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE missions
   SET owner_user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_user_id)
 WHERE owner_user_id IS NOT NULL AND owner_user_id IN (SELECT old_uid FROM _uid_map);

UPDATE missions
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE agent_runs
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE agent_runs
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE user_preferences
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE provider_profiles
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IN (SELECT old_uid FROM _uid_map);

UPDATE provider_profiles
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE swarm_events
   SET user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = user_id)
 WHERE user_id IS NOT NULL AND user_id IN (SELECT old_uid FROM _uid_map);

UPDATE swarm_events
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE workspace_memory
   SET owner_user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_user_id)
 WHERE owner_user_id != '' AND owner_user_id IN (SELECT old_uid FROM _uid_map);

UPDATE workspace_settings
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id != '' AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE session_summaries
   SET owner_user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_user_id)
 WHERE owner_user_id != '' AND owner_user_id IN (SELECT old_uid FROM _uid_map);

UPDATE session_summaries
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE learned_patterns
   SET owner_user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_user_id)
 WHERE owner_user_id != '' AND owner_user_id IN (SELECT old_uid FROM _uid_map);

UPDATE instincts
   SET owner_user_id = (SELECT new_uid FROM _uid_map WHERE old_uid = owner_user_id)
 WHERE owner_user_id != '' AND owner_user_id IN (SELECT old_uid FROM _uid_map);

UPDATE teams
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE team_members
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE eval_runs
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE audit_governance
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

UPDATE audit_bash
   SET workspace_id = (SELECT new_wid FROM _wid_map WHERE old_wid = workspace_id)
 WHERE workspace_id IS NOT NULL AND workspace_id IN (SELECT old_wid FROM _wid_map);

-- ── 4. Recreate users table without username ──────────────────────────────────
-- username had a UNIQUE constraint so ALTER TABLE DROP COLUMN is not permitted
-- in SQLite. We use the standard SQLite table-reconstruction pattern instead.

CREATE TABLE users_v43 (
    user_id       TEXT PRIMARY KEY,
    email         TEXT UNIQUE,
    role          TEXT NOT NULL DEFAULT 'user',
    team          TEXT,
    status        TEXT NOT NULL DEFAULT 'active',
    password_hash TEXT,
    created_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

INSERT INTO users_v43 (user_id, email, role, team, status, password_hash, created_at, updated_at)
SELECT user_id, email, role, team, status, password_hash, created_at, updated_at
FROM users;

DROP TABLE users;
ALTER TABLE users_v43 RENAME TO users;

-- ── 5. Cleanup ────────────────────────────────────────────────────────────────

DROP TABLE _uid_map;
DROP TABLE _wid_map;
