-- Phase 50: add parent_swarm_id to swarm_events to support manager-led federation.
-- Existing rows get NULL (top-level / silo swarms).

ALTER TABLE swarm_events ADD COLUMN parent_swarm_id TEXT;

CREATE INDEX IF NOT EXISTS ix_swarm_events_parent ON swarm_events(parent_swarm_id);
