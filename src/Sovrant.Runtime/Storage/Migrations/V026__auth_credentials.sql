-- Phase 85: Identity & Login Parity
-- Adds password-based auth, sliding-window token TTL, server settings, and
-- admin-generated one-time password reset tokens.

-- Password authentication on users
ALTER TABLE users ADD COLUMN password_hash TEXT;

-- Sliding-window TTL support on tokens
ALTER TABLE api_tokens ADD COLUMN last_used_at TEXT;

-- Server-wide settings (key/value) — bootstrapped by IIdentityService
CREATE TABLE IF NOT EXISTS server_settings (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

-- One-time password reset tokens (admin-generated, 24-hour TTL)
CREATE TABLE IF NOT EXISTS password_reset_tokens (
    token_id   TEXT PRIMARY KEY,
    user_id    TEXT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash TEXT NOT NULL UNIQUE,
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    expires_at TEXT NOT NULL,
    used_at    TEXT
);
CREATE INDEX IF NOT EXISTS ix_prt_user ON password_reset_tokens(user_id);
CREATE INDEX IF NOT EXISTS ix_prt_hash ON password_reset_tokens(token_hash);
