-- V009: Backfill user_id = '' rows to the install's boot identity.
--
-- sessions, token_usage, and credentials were all created in V001/V002 with
-- `user_id TEXT NOT NULL DEFAULT ''`. Any row written before Phase 38 tagged
-- sessions with a real user_id will have '' in that column, which makes them
-- invisible to every ownership-scoped query added in Phase 37/38.
--
-- We backfill those rows to the oldest admin user in the users table. On a
-- single-user install (the common case) that is the boot identity. On a
-- multi-user install '' rows are extremely rare anyway because Phase 38
-- enforces a non-empty user_id at insert time.
--
-- If the users table is empty (e.g. a fresh DB has no '' rows to backfill
-- yet because migrations run before SeedDefaultUser) the UPDATEs are no-ops.
-- This migration is intentionally idempotent and safe to re-run.

-- Pick a single deterministic target user: the oldest active admin, then
-- the oldest active user, then any user. Stored in a temp table so we
-- evaluate it once and reuse it across the three UPDATEs below.

CREATE TEMP TABLE _v009_target AS
SELECT user_id FROM users
WHERE status = 'active'
ORDER BY
    CASE role WHEN 'admin' THEN 0 ELSE 1 END,
    created_at ASC
LIMIT 1;

-- Only run the backfill if we actually found a target user.

UPDATE sessions
SET user_id = (SELECT user_id FROM _v009_target)
WHERE user_id = ''
  AND (SELECT COUNT(*) FROM _v009_target) = 1;

UPDATE token_usage
SET user_id = (SELECT user_id FROM _v009_target)
WHERE user_id = ''
  AND (SELECT COUNT(*) FROM _v009_target) = 1;

UPDATE credentials
SET user_id = (SELECT user_id FROM _v009_target)
WHERE user_id = ''
  AND (SELECT COUNT(*) FROM _v009_target) = 1;

DROP TABLE _v009_target;
