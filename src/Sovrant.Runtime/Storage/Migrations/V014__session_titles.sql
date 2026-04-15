-- V014: Session titles for naming and search.
--
-- Adds a `title` column to the sessions table so users can name their
-- conversations. Auto-generated from the first user message when not
-- explicitly set via /rename.

ALTER TABLE sessions ADD COLUMN title TEXT;

CREATE INDEX ix_sessions_title ON sessions (title) WHERE title IS NOT NULL;
