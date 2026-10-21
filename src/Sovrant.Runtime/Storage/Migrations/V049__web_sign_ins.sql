-- V049: per-browser Web sign-ins (roadmap Phase 145). Additive only.
--
-- One row per browser a person is signed in on. The browser holds a random
-- token in an HttpOnly cookie; only its SHA-256 hash is stored here. A sign-in
-- ends when it's revoked (sign out, sign out everywhere, or an admin), when
-- it has been idle longer than the idle timeout (not for "Keep me signed in"),
-- or at expires_at (the absolute limit, or the remember-me lifetime). The
-- timeouts themselves are configuration, not data, so they're applied by the
-- service, not stored per row.
CREATE TABLE web_sign_ins (
    sign_in_id      TEXT PRIMARY KEY,
    user_id         TEXT NOT NULL REFERENCES users(user_id) ON DELETE CASCADE,
    token_hash      TEXT NOT NULL UNIQUE,
    remember        INTEGER NOT NULL DEFAULT 0,
    user_agent      TEXT,
    ip_address      TEXT,
    created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    last_active_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    expires_at      TEXT NOT NULL,
    revoked_at      TEXT,
    revoked_reason  TEXT
);

CREATE INDEX ix_web_sign_ins_user ON web_sign_ins(user_id, revoked_at);
