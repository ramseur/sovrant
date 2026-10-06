-- Conversation folders (roadmap Phase 133) — mirrors the V048 block in
-- db/postgres/PostgresSchema.sql (used by Sovrant's built-in Postgres/Supabase
-- initializer) and V048__session_folders.sql (SQLite). Additive only.
--
-- This file is not read by any Sovrant runtime code — it exists for anyone
-- who set up their Supabase project via the Supabase CLI's own migration
-- history instead of Sovrant's built-in "Initialize" action. Every statement
-- is guarded (IF NOT EXISTS), so it's safe to apply on a project that already
-- picked these objects up through PostgresSchema.sql.


CREATE TABLE IF NOT EXISTS session_folders (
    folder_id        TEXT PRIMARY KEY,
    owner_user_id    TEXT NOT NULL,
    parent_folder_id TEXT REFERENCES session_folders(folder_id),
    name             TEXT NOT NULL,
    sort_order       INTEGER NOT NULL DEFAULT 0,
    created_at       TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"')),
    updated_at       TEXT NOT NULL DEFAULT (to_char(NOW() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
);

CREATE INDEX IF NOT EXISTS ix_session_folders_tree ON session_folders(owner_user_id, parent_folder_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_session_folders_sibling
    ON session_folders(owner_user_id, COALESCE(parent_folder_id, ''), lower(name));

ALTER TABLE sessions ADD COLUMN IF NOT EXISTS folder_id TEXT REFERENCES session_folders(folder_id) ON DELETE SET NULL;
CREATE INDEX IF NOT EXISTS ix_sessions_folder ON sessions(user_id, folder_id);

ALTER TABLE agent_runs ADD COLUMN IF NOT EXISTS session_id TEXT;
CREATE INDEX IF NOT EXISTS ix_agent_runs_session ON agent_runs(session_id);

