-- V041: Add owner_user_id and is_private to workspace_memory for per-user privacy.
-- Existing rows get owner_user_id = '' and is_private = 0 (public) — backwards-compatible.

ALTER TABLE workspace_memory ADD COLUMN owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE workspace_memory ADD COLUMN is_private    INTEGER NOT NULL DEFAULT 0;

CREATE INDEX ix_workspace_memory_owner ON workspace_memory(owner_user_id);
