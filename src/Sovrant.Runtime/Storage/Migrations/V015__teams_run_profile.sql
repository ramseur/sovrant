-- V015: Phase 78 Path 2 — per-team run profile.
--
-- Teams gain six columns that control *how* the team runs:
--   run_mode               — sequential | parallel | swarm
--   max_concurrent         — upper bound on parallel member execution
--   file_locks_enabled     — acquire per-task file locks during the run
--   quality_gate_enabled   — run post-execution quality review
--   quality_gate_threshold — 0–10 score threshold below which a retry triggers
--   decomposition_mode     — off | role-aware | open
--
-- Migration-time defaults are deliberately pessimistic: every pre-existing
-- team preserves single-member-at-a-time semantics until its owner opts
-- into the new capabilities. Newly-created teams inherit from the global
-- .sovrant/swarm.json defaults at creation time (wired in the next commit).

ALTER TABLE teams ADD COLUMN run_mode               TEXT    NOT NULL DEFAULT 'sequential';
ALTER TABLE teams ADD COLUMN max_concurrent         INTEGER NOT NULL DEFAULT 1;
ALTER TABLE teams ADD COLUMN file_locks_enabled     INTEGER NOT NULL DEFAULT 0;
ALTER TABLE teams ADD COLUMN quality_gate_enabled   INTEGER NOT NULL DEFAULT 0;
ALTER TABLE teams ADD COLUMN quality_gate_threshold INTEGER NOT NULL DEFAULT 7;
ALTER TABLE teams ADD COLUMN decomposition_mode     TEXT    NOT NULL DEFAULT 'off';
