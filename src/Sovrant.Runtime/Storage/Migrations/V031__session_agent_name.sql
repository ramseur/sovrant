-- V031: store the agent name bound to a session so it can be restored on resume.
ALTER TABLE sessions ADD COLUMN agent_name TEXT;
CREATE INDEX IF NOT EXISTS idx_sessions_agent_name ON sessions (agent_name) WHERE agent_name IS NOT NULL;
