-- V023: HTTP transport support for MCP servers.
--
-- Adds two columns to mcp_servers so a server can be configured as either:
--   * stdio  — command/args (existing behaviour, url IS NULL)
--   * http   — endpoint URL with optional headers (e.g. Authorization: Bearer …)
--
-- headers_json is masked in UI surfaces by the same heuristic that masks env
-- vars (keys containing "key", "secret", "token", or "auth").

ALTER TABLE mcp_servers ADD COLUMN url          TEXT;
ALTER TABLE mcp_servers ADD COLUMN headers_json TEXT NOT NULL DEFAULT '{}';
