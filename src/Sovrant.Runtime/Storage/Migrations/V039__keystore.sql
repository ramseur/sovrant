-- V039: move master encryption key from .keystore file into the DB.
-- SqliteCredentialStore reads key_hex from this table on startup.
-- The legacy file (~/.sovrant/credentials/.keystore) is migrated in on first
-- boot and then deleted; new installs never create the file at all.

CREATE TABLE IF NOT EXISTS keystore (
    scope      TEXT NOT NULL PRIMARY KEY,  -- 'default' (reserved for future per-workspace keys)
    key_hex    TEXT NOT NULL,              -- 64-char lowercase hex (256-bit AES master key)
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
