-- V021 (Phase 88-B): Provider profiles, replacing ~/.sovrant/providers.json.
--
-- One row per saved provider configuration (OpenAI, OpenRouter, Anthropic,
-- Ollama, …). The actual API key is *never* stored here — only a
-- credential_id reference into the encrypted ICredentialStore. Profiles
-- are per-user; the active profile id lives in user_preferences under
-- key 'provider.active_profile_id' (V020).

CREATE TABLE provider_profiles (
    profile_id     TEXT PRIMARY KEY,
    user_id        TEXT NOT NULL,
    name           TEXT NOT NULL,
    provider_kind  TEXT NOT NULL,
    base_url       TEXT NOT NULL,
    default_model  TEXT,
    max_tokens     INTEGER,
    credential_id  TEXT NOT NULL,
    created_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE INDEX provider_profiles_user_idx ON provider_profiles(user_id);
