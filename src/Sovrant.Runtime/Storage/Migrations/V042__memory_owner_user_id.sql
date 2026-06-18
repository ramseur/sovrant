-- V042: Add owner_user_id to session_summaries, learned_patterns, and instincts
-- so auto-generated memories are user-scoped and not mixed across users on the web.
-- Existing rows default to '' (public / unowned) — still injected for all users.
ALTER TABLE session_summaries ADD COLUMN owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE learned_patterns  ADD COLUMN owner_user_id TEXT NOT NULL DEFAULT '';
ALTER TABLE instincts          ADD COLUMN owner_user_id TEXT NOT NULL DEFAULT '';

CREATE INDEX ix_session_summaries_owner ON session_summaries(owner_user_id);
CREATE INDEX ix_learned_patterns_owner  ON learned_patterns(owner_user_id);
CREATE INDEX ix_instincts_owner         ON instincts(owner_user_id);
