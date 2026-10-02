-- V048: conversation folders (roadmap Phase 133). Additive only.
--
-- session_folders is an adjacency list: each folder stores its parent's id,
-- NULL meaning top level. Folders are per user and span every workspace. The
-- service layer enforces the tree rules (depth limit 5, no cycles, deleting a
-- folder moves its contents up to its parent), so parent_folder_id has no
-- ON DELETE CASCADE: one delete must never silently wipe a whole subtree, and
-- with foreign keys on, deleting a folder that still has subfolders fails here.
CREATE TABLE session_folders (
    folder_id        TEXT PRIMARY KEY,
    owner_user_id    TEXT NOT NULL,
    parent_folder_id TEXT REFERENCES session_folders(folder_id),
    name             TEXT NOT NULL,
    sort_order       INTEGER NOT NULL DEFAULT 0,
    created_at       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX ix_session_folders_tree ON session_folders(owner_user_id, parent_folder_id);

-- Sibling names are unique, ignoring case. COALESCE because NULLs never
-- collide in a UNIQUE index, so two top-level "Research" folders would
-- otherwise both be allowed.
CREATE UNIQUE INDEX ux_session_folders_sibling
    ON session_folders(owner_user_id, COALESCE(parent_folder_id, ''), name COLLATE NOCASE);

-- One folder per conversation. SET NULL is a safety net only — the service
-- moves a deleted folder's conversations to its parent before deleting it.
ALTER TABLE sessions ADD COLUMN folder_id TEXT REFERENCES session_folders(folder_id) ON DELETE SET NULL;
CREATE INDEX ix_sessions_folder ON sessions(user_id, folder_id);

-- Every run records the conversation it belongs to, so a chat's sidebar
-- label can show the swarm/team runs it launched. NULL for runs created
-- before this migration and for runs started outside any conversation.
ALTER TABLE agent_runs ADD COLUMN session_id TEXT;
CREATE INDEX ix_agent_runs_session ON agent_runs(session_id);
