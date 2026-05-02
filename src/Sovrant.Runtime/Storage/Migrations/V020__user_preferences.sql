-- V020 (Phase 88-A): Per-user preference store.
--
-- Replaces ~/.sovrant/settings.json fields (Model, BaseUrl, Provider,
-- MaxTokens, PermissionMode, IntentRouting, WebSearch, …). Schema mirrors
-- workspace_settings: a thin TEXT key/value store with last-write-wins.
-- The active provider profile id lands here too (key
-- "provider.active_profile_id"); the API key itself never appears in
-- this table — only the credential_id reference inside provider_profiles.

CREATE TABLE user_preferences (
    user_id    TEXT NOT NULL,
    key        TEXT NOT NULL,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    PRIMARY KEY (user_id, key)
);

CREATE INDEX user_preferences_key_idx ON user_preferences(key);
