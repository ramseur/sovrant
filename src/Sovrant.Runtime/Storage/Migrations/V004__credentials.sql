-- V004: Encrypted credential storage

CREATE TABLE credentials (
    key_hash     TEXT PRIMARY KEY,
    user_id      TEXT NOT NULL DEFAULT '',
    workspace_id TEXT,
    nonce        BLOB NOT NULL,
    tag          BLOB NOT NULL,
    ciphertext   BLOB NOT NULL,
    created_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
