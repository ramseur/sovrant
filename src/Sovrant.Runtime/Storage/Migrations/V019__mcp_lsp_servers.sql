-- V019: MCP and LSP server entries previously read from settings.json.
--
-- Metadata only (command/args/env/oauth metadata) lives here; secrets like
-- the OAuth client_secret and access tokens stay in the encrypted
-- credential store under keys "mcp.{name}.client_secret" and
-- "mcp.{name}.access_token".

CREATE TABLE mcp_servers (
    name              TEXT PRIMARY KEY,
    command           TEXT NOT NULL DEFAULT '',
    args_json         TEXT NOT NULL DEFAULT '[]',
    env_json          TEXT NOT NULL DEFAULT '{}',
    oauth_config_json TEXT,
    created_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);

CREATE TABLE lsp_servers (
    language   TEXT PRIMARY KEY,
    command    TEXT NOT NULL DEFAULT '',
    args_json  TEXT NOT NULL DEFAULT '[]',
    env_json   TEXT NOT NULL DEFAULT '{}',
    created_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now')),
    updated_at TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ', 'now'))
);
