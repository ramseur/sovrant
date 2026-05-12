-- V027: Workspace-scoped provider profiles.
--
-- Adds workspace_id to provider_profiles so admins can configure a shared
-- provider for all workspace members. NULL = personal/user-owned profile
-- (visible and editable by the owning user). Non-NULL = workspace profile
-- (usable by all workspace members; key hidden from non-admin members).
--
-- The active workspace provider is recorded in workspace_settings under
-- key 'provider.active_profile_id'. Resolution order at chat time:
--   1. workspace_settings[workspace_id]['provider.active_profile_id']
--   2. user_preferences[user_id]['provider.active_profile_id']

ALTER TABLE provider_profiles ADD COLUMN workspace_id TEXT;

CREATE INDEX IF NOT EXISTS provider_profiles_workspace_idx
    ON provider_profiles(workspace_id)
    WHERE workspace_id IS NOT NULL;
