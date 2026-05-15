-- Phase L (security): add user_id to swarm_events so ownership can be
-- verified on GET /v1/swarm/{id} and GET /v1/swarm/{id}/events.
-- Existing rows get NULL; new rows are stamped from SwarmExecutionContext.UserId.

ALTER TABLE swarm_events ADD COLUMN user_id TEXT;

CREATE INDEX IF NOT EXISTS ix_swarm_events_user ON swarm_events(user_id);
