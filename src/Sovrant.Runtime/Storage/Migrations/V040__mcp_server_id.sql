-- V040: Add stable id column to mcp_servers for workspace-scoped gating.
-- The name column remains the natural key for command routing and credential
-- store lookups (mcp.{name}.*). The id column is a stable surrogate that
-- survives renames so workspace_settings can reference MCPs by id, not name.

ALTER TABLE mcp_servers ADD COLUMN id TEXT NOT NULL DEFAULT '';
UPDATE mcp_servers SET id = lower(hex(randomblob(16)));
CREATE UNIQUE INDEX idx_mcp_servers_id ON mcp_servers(id);
