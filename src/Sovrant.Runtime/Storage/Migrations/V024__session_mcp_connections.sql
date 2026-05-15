-- V024: Per-session MCP connection gating.
--
-- Adds an mcp_servers TEXT column to the sessions table holding a JSON array
-- of MCP server names whose tools are exposed to the LLM for that session.
--
-- Semantics:
--   NULL          — no gating; every connected server's tools are available
--   '[]'          — all MCP tools disabled
--   '["a","b"]'   — only tools registered from servers 'a' and 'b' are exposed
--
-- The runtime applies this filter on every turn so users only get the MCPs
-- they explicitly opted into for that conversation.

ALTER TABLE sessions ADD COLUMN mcp_servers TEXT;
