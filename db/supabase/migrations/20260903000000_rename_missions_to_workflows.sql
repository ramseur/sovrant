-- Rename the mission layer to "workflows" — mirrors the guarded rename in
-- db/postgres/PostgresSchema.sql (used by Sovrant's built-in Postgres/
-- Supabase initializer) and V047__rename_missions_to_workflows.sql (SQLite).
-- No behavior change: same tables, same columns, just renamed.
--
-- This file is not read by any Sovrant runtime code — it exists for anyone
-- who set up their Supabase project via the Supabase CLI's own migration
-- history instead of Sovrant's built-in "Initialize" action. Per the
-- Supabase CLI's migration model (like SQLite's V0XX files), an already-
-- applied timestamped migration is treated as immutable history, so this
-- ships as a new file rather than editing 20260625000000_initial_schema.sql.
--
-- Guarded, not a bare ALTER, so it's safe to apply regardless of whether
-- a given project already ran a rename via db/postgres/PostgresSchema.sql
-- through some other path.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mission_scratchpad')
       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'workflow_scratchpad') THEN
        ALTER TABLE mission_scratchpad RENAME TO workflow_scratchpad;
        ALTER TABLE workflow_scratchpad RENAME COLUMN mission_id TO workflow_id;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'missions')
       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'workflows') THEN
        ALTER TABLE missions RENAME TO workflows;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'mission_events')
       AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'workflow_events') THEN
        ALTER TABLE mission_events RENAME TO workflow_events;
        ALTER TABLE workflow_events RENAME COLUMN mission_id TO workflow_id;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_missions_status') THEN
        ALTER INDEX ix_missions_status RENAME TO ix_workflows_status;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_missions_workspace') THEN
        ALTER INDEX ix_missions_workspace RENAME TO ix_workflows_workspace;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_missions_project') THEN
        ALTER INDEX ix_missions_project RENAME TO ix_workflows_project;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_missions_owner') THEN
        ALTER INDEX ix_missions_owner RENAME TO ix_workflows_owner;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_missions_is_private') THEN
        ALTER INDEX ix_missions_is_private RENAME TO ix_workflows_is_private;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_mission_events_mission') THEN
        ALTER INDEX ix_mission_events_mission RENAME TO ix_workflow_events_workflow;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_mission_events_workspace') THEN
        ALTER INDEX ix_mission_events_workspace RENAME TO ix_workflow_events_workspace;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_mission_scratchpad_mission') THEN
        ALTER INDEX ix_mission_scratchpad_mission RENAME TO ix_workflow_scratchpad_workflow;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'ix_mission_scratchpad_workspace') THEN
        ALTER INDEX ix_mission_scratchpad_workspace RENAME TO ix_workflow_scratchpad_workspace;
    END IF;
END $$;
