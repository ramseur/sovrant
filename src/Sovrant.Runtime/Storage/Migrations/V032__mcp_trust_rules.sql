-- V032: MCP quality and trust gates — per-server tool call policy rules.
-- workspace_id is used for access-control scoping (only workspace owners/admins
-- may write rules); evaluation loads rules by server_name across all workspaces.
CREATE TABLE IF NOT EXISTS mcp_trust_rules (
    rule_id      TEXT NOT NULL PRIMARY KEY,
    workspace_id TEXT NOT NULL,
    server_name  TEXT NOT NULL,   -- exact server name or '*' (all servers)
    tool_pattern TEXT NOT NULL,   -- glob: 'delete_*', 'bulk_*', '*'
    action       TEXT NOT NULL,   -- Allow | RequireConfirmation | Block
    reason       TEXT,            -- surfaced to agent on block/confirmation
    created_at   TEXT NOT NULL,
    updated_at   TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_mcp_trust_rules_workspace
    ON mcp_trust_rules (workspace_id);

CREATE INDEX IF NOT EXISTS idx_mcp_trust_rules_server
    ON mcp_trust_rules (server_name);
