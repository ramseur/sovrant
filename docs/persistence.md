# Sovrant — Persistence Layer

**Phases 32, 35, 36, 37, 37.5, 42.5, 51, 52, 55, 57, 78, 85, 87, 88, 90, 93** | **Last updated:** 2026-05-09

This document describes how Sovrant stores durable operational data. All persistent state (sessions, memory, audit, credentials, token usage, workspaces, projects, users) is managed by a SQLite database. Flat-file stores (JSONL, JSON) remain available as a dual-write option during migration, but they are now considered legacy and will be consolidated as part of Phase 42.5.

---

## Architecture Overview

```
                     ┌───────────────────────────────────┐
                     │          IStorageProvider          │
                     │   (lifecycle + migrations)         │
                     └──────────────┬────────────────────┘
                                    │
                     ┌──────────────▼────────────────────┐
                     │      SqliteStorageProvider         │
                     │  ~/.sovrant/data/sovrant.db        │
                     │  WAL mode · FK · busy_timeout      │
                     └──────────────┬────────────────────┘
                                    │
               ISqliteConnectionFactory (internal)
                                    │
   ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐
   ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼
ISession IMemory IAudit IToken ICredl IWork-  IProject IUser  Eval  Swarm  Runtime Mission  Team
Store    Store   Store  Usage  Store  space   Service  Service Store Event  Trace   Store   Store
                        Store         Service                  Store Store
```

Every domain store receives an `ISqliteConnectionFactory` via constructor injection, creates a new connection per operation, and uses parameterized queries exclusively (no string interpolation in SQL). The provider is registered once as a singleton and exposed under both `IStorageProvider` and `ISqliteConnectionFactory` so all stores share the same instance and connection cache.

---

## Database Location

| Scenario | Path |
|---|---|
| Default (CLI + server) | `~/.sovrant/data/sovrant.db` |
| Custom | Set `SOVRANT_DB_PATH` environment variable |
| Tests | Temp file (`%TEMP%/sovrant_test_{guid}.db`, deleted on test dispose) |

Resolution order (`SqliteStorageProvider` constructor):
1. Explicit `dbPath` argument (used by tests).
2. `SOVRANT_DB_PATH` environment variable.
3. Default: `{UserProfile}/.sovrant/data/sovrant.db`.

The data directory is created automatically on first run. If the directory cannot be created or the database cannot be opened, Sovrant logs an `ERROR` (`"Failed to initialize SQLite storage at {DbPath}. The application will continue but data will not be persisted."`) and continues — no crash on a bad path. **This graceful degradation is by design but can mask broken installs.** Production installs should set `SOVRANT_DB_REQUIRE=true` (Phase 42.5) so init failures throw `InvalidOperationException` at boot instead. See [DB Upgrades](#db-upgrades-phase-425) for the full lifecycle flow.

---

## Schema Migrations

Migrations are embedded SQL resources named `V{NNN}__{description}.sql` inside the `Sovrant.Runtime` assembly. The `MigrationRunner` applies them in version order and tracks each in a `schema_version` table with SHA-256 checksums.

| Version | File | What it adds |
|---|---|---|
| V001 | `V001__foundation.sql` | `schema_version`, `users`, `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `projects`, `project_members`, `project_config`, `config`, `api_tokens`, `roles`, `permissions`, `role_permissions`, `user_roles`, `audit_governance`, `audit_bash` |
| V002 | `V002__sessions.sql` | `sessions`, `session_entries`, `session_entries_fts` (FTS5), `token_usage` |
| V003 | `V003__memory.sql` | `session_summaries`, `learned_patterns`, `instincts` |
| V004 | `V004__credentials.sql` | `credentials` |
| V005 | `V005__swarm_evals.sql` | `swarm_events`, `eval_runs`, `eval_results` |
| V006 | `V006__workspaces.sql` | `workspace_memory` table; `IF NOT EXISTS` indexes on `sessions`, `token_usage`, `session_summaries`, `audit_governance`, `audit_bash`, `swarm_events`, `eval_runs` keyed on `workspace_id` |
| V007 | `V007__projects.sql` | `IF NOT EXISTS` indexes on `sessions(project_id)`, `token_usage(project_id)`, `workspace_memory(workspace_id, project_id)`, `project_members(user_id)` |
| V008 | `V008__backfill_orphan_workspaces.sql` | One-time data backfill: sets `workspace_id = 'ws-personal-' \|\| user_id` on orphan rows in `sessions`, `token_usage`, `credentials`, then propagates the new value to `audit_governance`, `audit_bash`, `session_summaries` via `session_id` joins. Only fills rows where the matching `ws-personal-{user_id}` workspace already exists; rows for users without one are left alone. |
| V009 | `V009__backfill_empty_user_ids.sql` | Phase 42.5 one-time data backfill for legacy `user_id = ''` rows in `sessions`, `token_usage`, `credentials`. Picks the oldest active admin user as the backfill target (deterministic without reading env vars); if no active admin exists, the migration is a no-op and leaves the empty strings in place. |
| V010 | `V010__runtime_traces.sql` | Phase 51 engine layer: `runtime_traces` (append-only structured reasoning trace for `IExecutor` state transitions — crash recovery walks this table), `mission_scratchpad` (typed shared store for parallel sub-agents within one mission). Both workspace/project-scoped. |
| V011 | `V011__missions.sql` | Phase 51 mission layer: `missions` (durable mission record with goal, status, plan JSON, workspace/project scoping), `mission_events` (append-only journal of mission state transitions — fully reconstructable history). |
| V012 | `V012__unified_orchestration.sql` | Phase 52 unified agent orchestration: `teams` (DB-backed team registry replacing `InMemoryTeamRegistry`), `team_members` (persistent members with role, template, tools, model level), `agent_runs` (unified run ledger for delegations, swarm tasks, and mission steps). Extends `swarm_events` with `kind` and `run_id` columns. |
| V013 | `V013__coordination_mailbox.sql` | Phase 57 inter-agent coordination: `coordination_events` (typed mailbox for PM-to-PM messages between agent groups, with status tracking and delivery/acknowledgement timestamps), `group_pm_assignments` (maps each agent group to its PM agent template). Both workspace/project-scoped. |
| V014 | `V014__session_titles.sql` | Adds `sessions.title` column for nameable conversations (auto-generated from the first user message when not explicitly set via `/rename`). Adds `ix_sessions_title` partial index where `title IS NOT NULL`. |
| V015 | `V015__teams_run_profile.sql` | Phase 78 Path 2 — per-team run profile. Adds six columns to `teams`: `run_mode` (`sequential`/`parallel`/`swarm`, default `sequential`), `max_concurrent` (default 1), `file_locks_enabled` (default 0), `quality_gate_enabled` (default 0), `quality_gate_threshold` (0–10, default 7), `decomposition_mode` (`off`/`role-aware`/`open`, default `off`). Pessimistic defaults preserve single-member-at-a-time semantics for pre-existing teams; new teams inherit from global swarm defaults (stored in `workspace_settings` as `swarm.*` keys, editable via the Swarm Defaults panel in Web/Desktop). |
| V016 | `V016__session_entry_provider.sql` | Adds `session_entries.provider` column so loaded chats can render "Provider · Model" on assistant bubbles (parity with live streaming). |
| V017 | `V017__hooks.sql` | `hooks` table — one row per hook definition; replaces `.sovrant/hooks.json`. Web/Desktop UI is the canonical edit surface. |
| V018 | `V018__workspace_settings.sql` | `workspace_settings` table — workspace-scoped budgets, session TTL/cap, and runtime-mutable knobs that previously lived in env vars only. Convention: `workspace_id = ''` means "global / server default". |
| V019 | `V019__mcp_lsp_servers.sql` | `mcp_servers` and `lsp_servers` tables — MCP and LSP server entries previously read from `settings.json`. Metadata-only; secrets (OAuth `client_secret`, access tokens) remain in the encrypted credential store. |
| V020 | `V020__user_preferences.sql` | Phase 88-A — `user_preferences` table. Replaces `~/.sovrant/settings.json` fields (`Model`, `BaseUrl`, `Provider`, `MaxTokens`, `PermissionMode`, `IntentRouting`, `WebSearch`, …). Thin TEXT key/value store with last-write-wins. |
| V021 | `V021__provider_profiles.sql` | Phase 88-B — `provider_profiles` table. One row per saved provider configuration (OpenAI, OpenRouter, Anthropic, Ollama, …). API keys are **never** stored here — only a `credential_id` reference into the encrypted `ICredentialStore`. Phase 90-G plaintext-key migration completes this: any pre-existing plaintext keys move to the keystore on first launch. |
| V022 | `V022__workspace_identity_unification.sql` | Phase 87 Track D — workspace identity unification. Pre-Phase-87 callers wrote artifacts and DB rows under the bare sentinel `personal`; this migration normalizes those rows to the canonical `ws-personal-{user_id}` form minted by `SqliteWorkspaceStore.CreatePersonalWorkspaceAsync`. |
| V023 | `V023__mcp_http_transport.sql` | HTTP transport support for MCP servers. Adds `url` (TEXT) and `headers_json` (TEXT, default `{}`) columns to `mcp_servers` so a server can be stdio (url IS NULL) or HTTP (endpoint URL with optional masked auth headers). |
| V024 | `V024__session_mcp_connections.sql` | Per-session MCP connection gating. Adds `mcp_servers` (TEXT JSON array) to `sessions`. NULL = no gating; `[]` = all MCP tools disabled; `["a","b"]` = only tools from named servers exposed. Applied per-turn by the runtime. |
| V025 | `V025__swarm_events_user_id.sql` | Phase L security — adds `user_id` column to `swarm_events` for ownership verification on `GET /v1/swarm/{id}` and `GET /v1/swarm/{id}/events`. Existing rows get NULL; new rows stamped from `SwarmExecutionContext.UserId`. Adds `ix_swarm_events_user` index. |
| V026 | `V026__auth_credentials.sql` | Phase 85 Identity & Login Parity — adds `password_hash` to `users`, `last_used_at` to `api_tokens` for sliding-window TTL, and creates `server_settings` (key/value store bootstrapped by `IIdentityService`) and `password_reset_tokens` (admin-generated one-time tokens, 24-hour TTL) tables. |

All V006/V007 statements are **additive** (`CREATE TABLE`, `CREATE INDEX IF NOT EXISTS`), so a database created at V005 or earlier upgrades cleanly on next boot — no manual intervention. V008 then backfills any orphan rows from those upgraded databases, and V009 backfills any empty-string `user_id` rows left over from the pre-Phase-38 seeding flow.

Migrations are idempotent — running `InitializeAsync` multiple times is safe. The runner skips already-applied versions and records the SHA-256 checksum of each script in `schema_version.checksum`. **Checksum drift is enforced as of Phase 42.5**: if a previously-applied `V00X__*.sql` file has been edited in place, the embedded checksum no longer matches the stored one and `InitializeAsync` throws `MigrationDriftException` on the next boot. Rows stamped before Phase 42.5 landed have `checksum = NULL` and are tolerated so legacy installs upgrade cleanly — drift detection only triggers on rows that were recorded *with* a checksum.

### Future-proofing

The schema is designed upfront so that Phases 33–37 can ship without `ALTER TABLE`:

- **`workspace_id`** and **`project_id`** existed as nullable columns on sessions, token_usage, audit, credentials, memory, swarm, and eval tables in V001. V006/V007 only added indexes, never columns.
- **`users.role`**, **`users.team`**, **`users.status`** existed in V001. **Phase 37 added the API surface only — no schema changes.**
- **RBAC tables** (`roles`, `permissions`, `role_permissions`, `user_roles`) exist in V001 (still empty). Phase 40 will populate them.
- **`api_tokens`** table exists in V001 (still empty). Phase 38 will populate it.

---

## DB Upgrades (Phase 42.5)

This section covers the lifecycle of an in-place upgrade from any prior schema version to the current one. Every operational guarantee below is enforced by an automated test in `tests/Sovrant.Runtime.Tests/Storage/OldDbUpgradeTests.cs` — the "V005 → current (V026)" path is the one we exercise on every CI run, which transitively covers V001–V005 since the runner always applies migrations in order.

### When migrations run

`InitializeAsync` is invoked exactly once per process boot, from `InitializeRuntimeAsync` in the server and from `InitAsync` in the CLI. The flow is:

```
CreateConnection
     ↓
SetPragmas  (WAL, foreign_keys, busy_timeout, secure_delete)
     ↓
(optional) BackupBeforeUpgrade  ← if SOVRANT_DB_BACKUP_ON_UPGRADE=true AND pending > 0 AND current > 0
     ↓
MigrationRunner.RunPendingMigrations
     ├─ VerifyNoChecksumDrift  → throws MigrationDriftException if a stored checksum no longer matches
     └─ apply each pending migration inside its own transaction
     ↓
SeedDefaultUser  (INSERT OR IGNORE)
     ↓
SeedPersonalWorkspace  (INSERT OR IGNORE)
     ↓
HardenDbFilePermissions  (Unix chmod 600 on main + -wal + -shm)
```

Any failure at the migration step is caught and logged. With `SOVRANT_DB_REQUIRE=true` the failure is rethrown as `InvalidOperationException` so the process exits; without the flag the engine continues running with `SchemaVersion = 0` and no persistence. `MigrationDriftException` is always rethrown regardless of `SOVRANT_DB_REQUIRE` because continuing would mean running on an unknown schema.

### Recommended first-boot and upgrade procedure

1. **Before upgrading a production install**, set `SOVRANT_DB_BACKUP_ON_UPGRADE=true` *and* `SOVRANT_DB_REQUIRE=true`. The first flag makes the boot take a snapshot before mutating anything; the second flag makes any migration or backup failure halt the process instead of running silently.
2. **Run `sovrant db migrate --dry-run`** to see exactly which versions would be applied. This opens the existing DB read-only and lists pending migrations without touching them.
3. **Run `sovrant db status`** to record the current schema version and table row counts. Save the output so you can diff it against the post-upgrade state.
4. **Boot the new binary once** (CLI `sovrant db migrate` or a server start is enough). Migrations apply in insertion order inside per-version transactions, and the runner stamps each row in `schema_version` with its SHA-256 checksum.
5. **Verify** with `sovrant db status` and `sovrant db version`. If row counts regressed or `schema_version` is unexpected, **do not write to the DB** — restore the backup (see below).

### Rolling back

Rollback is always a file-level operation — SQLite migrations are forward-only. The backup created by `SOVRANT_DB_BACKUP_ON_UPGRADE` lives next to the main DB as `sovrant.db.bak-{previousVersion}` and is a bit-for-bit copy of the file *after* a WAL checkpoint, so it can replace the main DB in place:

```bash
# Stop every process that is using the DB first (server + any CLI).
cp ~/.sovrant/data/sovrant.db.bak-8   ~/.sovrant/data/sovrant.db
rm ~/.sovrant/data/sovrant.db-wal     2>/dev/null
rm ~/.sovrant/data/sovrant.db-shm     2>/dev/null
```

The `-wal` and `-shm` sidecars are deleted because they belong to the post-migration DB; leaving them in place will confuse SQLite on the next open. The backup itself was taken after `PRAGMA wal_checkpoint(TRUNCATE)` so all prior WAL contents are already folded into the main file.

A manual backup (outside the flag) uses the same mechanism:

```bash
sovrant db backup                      # → ~/.sovrant/data/sovrant.db.bak-{version}
sovrant db backup /var/tmp/snapshot.db # → custom path
```

Both forms run the checkpoint before copying.

### Migration drift

`schema_version.checksum` carries the SHA-256 of the migration SQL at the time it was applied. On every boot the runner recomputes the checksum of each embedded `V00X__*.sql` and compares it to the stored value. A mismatch throws `MigrationDriftException` (which is always rethrown even without `SOVRANT_DB_REQUIRE`) with a message naming the offending version.

To recover:
- **If drift is unintentional** (someone edited a V-numbered file that had already shipped): revert the edit so the embedded SQL matches the stored checksum again.
- **If the drift is intentional** (you need a correction to an already-shipped migration): add a *new* V-numbered migration that performs the correction. Editing an old migration is not supported.
- **Last resort for developer machines only**: restore from the backup, or manually update `schema_version.checksum` to the new value via `sqlite3` and document why.

Null stored checksums (legacy rows from before Phase 42.5) are tolerated — the runner treats them as "can't prove drift, assume clean" so no one is forced to re-stamp their existing DBs.

### Inspecting the DB

`sovrant db inspect <table> [--limit N]` prints `PRAGMA table_info` followed by the first N rows (default 20). The table name is validated against `sqlite_master` before being interpolated into the query, so it is safe against injection from the shell argument. Use this for post-upgrade spot checks instead of shelling out to `sqlite3`.

### Health endpoint coverage

`GET /health` now reports a `db` block alongside the overall status:

```json
{
  "status": "ok",
  "db": {
    "status": "ok",
    "schema_version": 9,
    "path": "/home/eramseur/.sovrant/data/sovrant.db",
    "error": null
  }
}
```

If the DB probe fails (disk full, permissions change, corrupt file), `db.status` flips to `"error"` and `status` flips to `"degraded"` — both are still returned with HTTP 200 so load balancers can distinguish "server down" from "server up but DB broken." The probe uses a read-only connection and a cheap `COUNT(*) FROM schema_version`, so it is safe to call from unauthenticated monitors.

---

## Database Inventory (authoritative)

The list below is generated from the migration scripts in `src/Sovrant.Runtime/Storage/Migrations/V0*.sql`. The current schema spans 26 migrations (V001–V026). V008, V009, V015, V016, V022, V025 ship no new application tables — V008/V009/V022 are data backfills/normalizations, V015 adds columns to `teams`, V016 adds a column to `session_entries`, V023 adds columns to `mcp_servers`, V024 adds a column to `sessions`, V025 adds a column to `swarm_events`. The migrations V017–V021 and V026 add the hooks, workspace settings, MCP/LSP server registry, user preferences, provider profiles, and auth credential tables that completed the move of all on-disk JSON config to SQLite (per the `.env`-only convention introduced in Phase 93).

### Tables by purpose

| Category | Tables | Migration | Notes |
|---|---|---|---|
| **Identity & access** | `users`, `api_tokens`, `roles`, `permissions`, `role_permissions`, `user_roles` | V001 | `users` is the only one populated today; the rest are placeholders for Phase 38 (api_tokens) and Phase 40 (RBAC). |
| **Workspaces** | `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory` | V001 + V006 | `workspace_memory` is the only addition in V006; the other four shipped in V001 as placeholders. |
| **Projects** | `projects`, `project_members`, `project_config` | V001 | All structural; V007 only adds indexes. |
| **Generic config** | `config` | V001 | Scoped key-value (`scope`, `key`, `value`). Used by global settings overrides. |
| **Sessions** | `sessions`, `session_entries`, `session_entries_fts` (+ 5 FTS5 internals), `token_usage` | V002 | `session_entries_fts` is a `CREATE VIRTUAL TABLE … USING fts5`. SQLite materializes 5 internal tables: `session_entries_fts_config`, `_data`, `_docsize`, `_idx`. Three triggers (`session_entries_ai/ad/au`) keep FTS in sync with `session_entries`. |
| **Memory** | `session_summaries`, `learned_patterns`, `instincts` | V003 | Three-layer agent memory. JSON arrays stored as TEXT (`tasks`, `tools_used`, `files_modified`, `evidence`). |
| **Credentials** | `credentials` | V004 | Encrypted blobs (nonce + tag + ciphertext columns). |
| **Swarm & evals** | `swarm_events`, `eval_runs`, `eval_results` | V005 | `swarm_events` is the canonical store for swarm history as of **Phase 37.5** — see [Swarm Event Store](#swarm-event-store-sqliteswarmeventstore--phase-375) below. |
| **Audit** | `audit_governance`, `audit_bash` | V001 | INTEGER PK AUTOINCREMENT, no FK to sessions. |
| **Engine traces** | `runtime_traces`, `mission_scratchpad` | V010 | Phase 51. `runtime_traces` is the append-only IExecutor state-transition log (crash recovery). `mission_scratchpad` is the typed shared store for parallel sub-agents. Both workspace/project-scoped. |
| **Missions** | `missions`, `mission_events` | V011 | Phase 51. Durable mission state + append-only event journal. Mission history is fully reconstructable from `mission_events` alone. |
| **Unified orchestration** | `teams`, `team_members`, `agent_runs` | V012 | Phase 52. DB-backed team registry, persistent members, unified run ledger across delegations/swarm-tasks/mission-steps. `swarm_events` extended with `kind` + `run_id`. |
| **Inter-agent coordination** | `coordination_events`, `group_pm_assignments` | V013 | Phase 57. Typed mailbox for PM-to-PM coordination between agent groups. Events carry status (pending/delivered/acknowledged), workspace/project scoping. |
| **Session metadata** | columns on `sessions`, `session_entries` | V014, V016 | V014 adds `sessions.title` (nullable) for nameable conversations + `ix_sessions_title` partial index. V016 adds `session_entries.provider` so loaded chats can render "Provider · Model" on assistant bubbles. |
| **Team run profile** | columns on `teams` | V015 | Phase 78 Path 2. Six columns govern team execution: `run_mode`, `max_concurrent`, `file_locks_enabled`, `quality_gate_enabled`, `quality_gate_threshold`, `decomposition_mode`. Pessimistic defaults preserve pre-Phase-78 single-member-at-a-time semantics. |
| **Migration metadata** | `schema_version` | bootstrapped by `MigrationRunner` | Stores version, `applied_at`, and SHA-256 `checksum` of each script. |

### Foreign-key topology

```
users ──┬── workspaces.owner_id           (RESTRICT — blocks hard-delete)
        ├── workspace_members.user_id     (RESTRICT)
        ├── workspace_invites             (no direct FK, email-based)
        ├── project_members.user_id       (RESTRICT)
        ├── api_tokens.user_id            (RESTRICT — Phase 38)
        └── user_roles.user_id            (RESTRICT — Phase 40)

workspaces ──┬── workspace_members.workspace_id
             ├── workspace_config.workspace_id
             ├── workspace_invites.workspace_id
             ├── workspace_memory.workspace_id    (CASCADE on delete)
             └── projects.workspace_id            (NULLable)

projects ──┬── project_members.project_id
           └── project_config.project_id

sessions ──── session_entries.session_id          (CASCADE on delete)
eval_runs ──── eval_results.run_id                (CASCADE on delete)
teams ──── team_members.team_id                   (CASCADE on delete)
```

`sessions`, `token_usage`, `audit_governance`, `audit_bash`, `credentials`, `session_summaries`, `swarm_events`, and `eval_runs` all carry `workspace_id` / `project_id` as **nullable, unconstrained TEXT** columns — no FK, by design, so legacy rows from before workspaces existed remain valid.

### Indexes after V001–V007

| Table | Indexes |
|---|---|
| `api_tokens` | `ix_api_tokens_user`, `ix_api_tokens_hash` |
| `audit_bash` | `ix_audit_bash_session`, `ix_audit_bash_workspace` (V006), `ix_audit_bash_project` (V007) |
| `audit_governance` | `ix_audit_governance_session`, `ix_audit_governance_workspace` (V006), `ix_audit_governance_project` (V007) |
| `eval_results` | `ix_eval_results_run` |
| `eval_runs` | `ix_eval_runs_suite`, `ix_eval_runs_workspace` (V006) |
| `learned_patterns` | `ix_learned_patterns_project` (legacy text "project" column, not `project_id`) |
| `project_members` | `ix_project_members_user` (V007) |
| `projects` | `ix_projects_workspace` (V007) |
| `session_entries` | `ix_session_entries_session` |
| `session_summaries` | `ix_session_summaries_project`, `ix_session_summaries_workspace` (V006) |
| `sessions` | `ix_sessions_user`, `ix_sessions_status`, `ix_sessions_workspace` (V006), `ix_sessions_project` (V007) |
| `swarm_events` | `ix_swarm_events_swarm`, `ix_swarm_events_workspace` (V006), `ix_swarm_events_project` (V007) |
| `token_usage` | `ix_token_usage_session`, `ix_token_usage_user`, `ix_token_usage_workspace` (V006), `ix_token_usage_project` (V007) |
| `workspace_memory` | `ix_workspace_memory_workspace` (V006), `ix_workspace_memory_layer` (V006), `ix_workspace_memory_project` (V007) |
| `runtime_traces` | `ix_runtime_traces_run` (V010), `ix_runtime_traces_entry_type` (V010), `ix_runtime_traces_workspace` (V010), `ix_runtime_traces_project` (V010) |
| `mission_scratchpad` | `ix_mission_scratchpad_mission` (V010), `ix_mission_scratchpad_workspace` (V010) |
| `missions` | `ix_missions_status` (V011), `ix_missions_workspace` (V011), `ix_missions_project` (V011), `ix_missions_owner` (V011) |
| `mission_events` | `ix_mission_events_mission` (V011), `ix_mission_events_workspace` (V011) |
| `teams` | `ix_teams_workspace` (V012), `ix_teams_project` (V012), `ix_teams_name` (V012) |
| `team_members` | `ix_team_members_team` (V012), `ix_team_members_workspace` (V012) |
| `agent_runs` | `ix_agent_runs_parent` (V012), `ix_agent_runs_team` (V012), `ix_agent_runs_workspace` (V012), `ix_agent_runs_user` (V012), `ix_agent_runs_kind` (V012), `ix_agent_runs_status` (V012) |
| `coordination_events` | `ix_coordination_events_target` (V013), `ix_coordination_events_source` (V013), `ix_coordination_events_ws` (V013) |
| `group_pm_assignments` | `ix_group_pm_assignments_ws` (V013) |
| `sessions` (V014) | `ix_sessions_title` (V014, partial: `WHERE title IS NOT NULL`) |

**61 indexes total at V016.** V015 (`teams_run_profile`) and V016 (`session_entries.provider`) add only columns, no new indexes — column-level filters on `run_mode` or `provider` go through full table scans, which is fine at expected row counts. **Notably absent**: there is no index on `users.username` or `users.email` beyond the implicit unique constraint indexes — that is sufficient for lookups since SQLite auto-creates a B-tree for every `UNIQUE` column. There is also no covering index for the per-user audit join (`audit_governance` → `sessions(user_id)`); for now the join is small enough that it's not measurable, but it's listed under Phase 42.5.

### Triggers

| Trigger | Table | Purpose |
|---|---|---|
| `session_entries_ai` | `session_entries` | After insert: mirror new row into `session_entries_fts` |
| `session_entries_ad` | `session_entries` | After delete: tombstone the row in FTS |
| `session_entries_au` | `session_entries` | After update: tombstone old + insert new |

These three triggers are the entire FTS5 sync layer. There are no triggers anywhere else in the schema — no `updated_at` triggers, no soft-delete triggers, no audit triggers. All `updated_at` columns are written explicitly by the application code.

---

## SQLite Configuration

Every connection opened via `SqliteStorageProvider.CreateConnection` applies these PRAGMAs in a single batch:

```sql
PRAGMA journal_mode = WAL;        -- persistent: written to the DB header (one-shot)
PRAGMA synchronous = NORMAL;      -- per-connection
PRAGMA foreign_keys = ON;         -- per-connection
PRAGMA busy_timeout = 5000;       -- per-connection (5s lock wait)
PRAGMA cache_size = -20000;       -- per-connection (20 MB page cache)
```

> **Pragma scope matters.** Only `journal_mode=WAL` is persisted in the database header — every other PRAGMA is a per-connection setting and reverts to SQLite defaults for any connection that doesn't run the batch. If you ever connect to the DB with `sqlite3` directly, with a one-off audit script, or with a third-party tool, **expect to see `synchronous=2`, `busy_timeout=0`, `cache_size=-2000`, `foreign_keys=0`** unless that tool also runs the batch. This is a frequent source of "the docs lied" confusion when comparing the file against this document.

**WAL mode** is critical for CLI + server sharing the same database file — multiple readers never block each other, and a single writer doesn't block readers. WAL persists across processes; it does not need to be re-set on every connection (the second `PRAGMA journal_mode=WAL` is a no-op).

---

## Domain Stores

### Session Store (`SqliteSessionStore`)

**Replaces:** `JsonlSessionStore` (JSONL append-log files)

| Interface | `ISessionStore` |
|---|---|
| Tables | `sessions`, `session_entries` |
| Features | Full-text search via `session_entries_fts` (FTS5 with auto-sync triggers) |

Operations:
- `AppendAsync` — creates the session row if needed, inserts the entry, updates `sessions.updated_at`
- `LoadAsync` — returns all entries for a session in insertion order
- `ListAsync` — returns all session IDs ordered by most recently updated

**Dual-write:** Set `SOVRANT_SESSION_JSONL=true` to also write to the legacy `~/.sovrant/sessions/{id}.jsonl` files during migration.

### Memory Store (`SqliteMemoryStore`)

**Replaces:** `FileMemoryStore` (JSON files in `~/.sovrant/memory/`)

| Interface | `IMemoryStore` |
|---|---|
| Tables | `session_summaries`, `learned_patterns`, `instincts` |
| Features | JSON arrays stored as TEXT columns (tasks, tools_used, files_modified, evidence) |

Three memory layers:
1. **Session summaries** — condensed records of past sessions, scoped by project
2. **Learned patterns** — project conventions with confidence scoring
3. **Instincts** — trigger-action pairs with confidence decay, reinforcement, and pruning

Instinct operations use SQLite's `json_insert()` to append evidence entries without full-row rewrites.

### Audit Store (`SqliteAuditStore`)

**Replaces:** `AuditLogger` (JSONL files in `~/.sovrant/audit/`)

| Interface | `IAuditStore` |
|---|---|
| Tables | `audit_governance`, `audit_bash` |
| Features | Indexed by `session_id` for quick lookups |

Records:
- Governance events — tool name, phase, action (block/warn/allow), rule, reason
- Bash commands — command text, session ID, exit code

**Dual-write:** Set `SOVRANT_AUDIT_JSONL=true` to also write to legacy JSONL files.

### Credential Store (`SqliteCredentialStore`)

**Replaces:** `AesGcmCredentialStore` (encrypted files in `~/.sovrant/credentials/`)

| Interface | `ICredentialStore` |
|---|---|
| Tables | `credentials` |
| Encryption | AES-256-GCM (same as file-based store) |

The master key is still stored on disk at `~/.sovrant/credentials/.keystore` (hex-encoded, user-only permissions on POSIX). Each credential row stores the nonce, authentication tag, and ciphertext as separate BLOB columns.

### Token Usage Store (`SqliteTokenUsageStore`)

**New** — previously tracked only in-memory via `SessionConfig.AddTokens`.

| Interface | `ITokenUsageStore` |
|---|---|
| Tables | `token_usage` |
| Features | Per-turn records with model, input/output tokens, optional cost |

Operations:
- `RecordAsync` — inserts a usage row per LLM turn
- `GetSessionTotalsAsync` — aggregates tokens and cost for a session

### Eval Result Store (`SqliteEvalResultStore`)

**Replaces:** `EvalResultStore` (file-based)

| Interface | `IEvalResultStore` |
|---|---|
| Tables | `eval_runs`, `eval_results` |
| Features | Suite-based eval runs with per-case pass/fail results, linked by `run_id` (CASCADE on delete) |

`SqliteEvalResultStore` is the primary implementation, using the `eval_runs` and `eval_results` tables from V005.

### Workspace Service (`SqliteWorkspaceStore`) — Phase 35

| Interface | `IWorkspaceService` |
|---|---|
| Tables | `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory` |
| Features | Personal workspace auto-create on user seed; team workspaces; invite tokens (32-byte random hex, 7-day expiry); per-workspace key-value config; aggregated token usage; layered memory entries (`pattern`, `instinct`, `summary`) |

Workspace IDs follow two formats:
- `ws-personal-{userId}` for personal workspaces (one per user, idempotent)
- `ws-{guid:N}` for team workspaces

Owners are implicitly added as `workspace_members` with `role='owner'` and cannot be removed.

### Project Service (`SqliteProjectStore`) — Phase 36

| Interface | `IProjectService` |
|---|---|
| Tables | `projects`, `project_members`, `project_config` |
| Features | Workspace-scoped CRUD; archive/unarchive (soft-delete via `archived_at`); explicit member list with `lead`/`contributor`/`viewer` roles; **open-by-default access** (no explicit members → all workspace members have access); 3-tier config inheritance (project → workspace → global); merged project + workspace memory views |

`HasAccessAsync` returns `true` when there are no explicit `project_members` rows for the project — projects start "open" to the entire workspace and become restricted as soon as you add the first explicit member.

### User Service (`SqliteUserStore`) — Phase 37

| Interface | `IUserService` |
|---|---|
| Tables | `users` (read/write); `sessions`, `token_usage`, `audit_governance` (read-only joins for derived views) |
| Features | Server-generated `usr_{16hex}` IDs; soft-delete only (`status='inactive'`, FK references preserved); strict input validation (slug regex, email shape, role/status whitelist); per-user profile with derived `session_count`, token totals, and `last_seen_at`; per-user usage with model + date filters; per-user audit join through `sessions.user_id` |

**Security boundary:** Phase 37 adds **no new authentication or authorization**. Every endpoint is gated by the existing single `SOVRANT_TOKEN` bearer middleware. Per-user bearer tokens, RBAC, and admin-only routes are explicitly deferred to Phase 38. The store does enforce:

- Slug regex `^[a-zA-Z0-9._-]{1,64}$` on usernames and any caller-provided `user_id`
- Email shape check + 254-char cap
- Role whitelist (`user`, `admin`)
- Status whitelist (`active`, `inactive`)
- Mass-assignment safety: route DTOs only expose `username`, `email`, `role`, `team`, `status`
- Hard-delete is **not** exposed; `DELETE /v1/users/{id}` flips `status='inactive'`
- The route layer refuses to deactivate the **server boot identity** (the `SOVRANT_USER_ID`/OS-username row) — would brick session seeding

**Coexistence with the seeded default user:** `SqliteStorageProvider.SeedDefaultUser` runs on every boot with `INSERT OR IGNORE` and uses the OS username as the `user_id`. New API-created users get `usr_{16hex}` IDs. Both formats are valid PKs into `users` and coexist safely. After `CreateAsync` succeeds, `UserRoutes.CreateUser` calls `IWorkspaceService.CreatePersonalWorkspaceAsync(newUser.UserId)` so new users get the same starting state as the seeded user.

**Known limitation (deferred to Phase 38):** Today every session is created with `user_id = Environment.UserName`, so sessions, token usage, and audit events for users created via the API will appear empty until per-user identity flows through `SqliteSessionStore`.

### Swarm Event Store (`SqliteSwarmEventStore`) — Phase 37.5

| Interface | `ISwarmEventStore` |
|---|---|
| Tables | `swarm_events` (read/write); indexes `ix_swarm_events_swarm`, `ix_swarm_events_workspace` (V006), `ix_swarm_events_project` (V007) |
| Features | Append-only event log per `swarm_id`; replay in insertion order; `ListSwarmsAsync` filtering by `workspace_id` / `project_id` / `limit`; per-event `agent_id` extraction so UIs can group by worker |

`SwarmSession` now writes through `ISwarmEventStore` instead of `~/.sovrant/swarm/sessions/{swarmId}.jsonl`. The contract is unchanged for callers — `RecordAsync`, `ReplayAsync`, `ListSessions`, `Exists` still exist — but the underlying rows land in the `swarm_events` table that has been waiting empty since V005.

`SwarmOrchestrator.ExecuteAsync` accepts a `SwarmExecutionContext(UserId, WorkspaceId, ProjectId)` that the server's `SwarmRoutes` populates from `WorkspaceContextMiddleware` (`HttpContext.Items["WorkspaceId"]` + `X-Project-Id` header). Every event written during a run is stamped with that scope, so `GET /v1/swarm/sessions?workspace_id=…&project_id=…` returns only the swarms a user is allowed to see.

**Migration of legacy data:** existing JSONL files are imported by:

```
sovrant db import-swarm [--dir <path>] [--delete-source]
```

The importer reads each `~/.sovrant/swarm/sessions/*.jsonl` file, classifies each line by the same property-sniffing rule the legacy `SwarmSession.DeserializeEvent` used, and inserts the row into `swarm_events` via `ISwarmEventStore`. Imported rows have `workspace_id` / `project_id` left null because legacy files were never scope-stamped. After a clean run, `--delete-source` removes the JSONL files.

---

## Dependency Injection

All stores are registered as singletons in `ServiceCollectionExtensions.AddSovrantRuntime()`:

```
SqliteStorageProvider  →  IStorageProvider + ISqliteConnectionFactory
SqliteSessionStore     →  ISessionStore      (or DualWriteSessionStore)
SqliteMemoryStore      →  IMemoryStore
SqliteAuditStore       →  IAuditStore        (or DualWriteAuditStore)
SqliteTokenUsageStore  →  ITokenUsageStore
SqliteCredentialStore  →  ICredentialStore
SqliteWorkspaceStore   →  IWorkspaceService                    (Phase 35)
SqliteProjectStore     →  IProjectService                      (Phase 36)
SqliteUserStore        →  IUserService                         (Phase 37)
SqliteSwarmEventStore  →  ISwarmEventStore                     (Phase 37.5)
SqliteRuntimeTraceStore      → IRuntimeTraceStore               (Phase 51)
SqliteMissionScratchpadStore → IMissionScratchpadStore          (Phase 51)
SqliteMissionStore           → IMissionStore                    (Phase 51)
SqliteTeamRegistry           → ITeamRegistry                    (Phase 52, replaces InMemoryTeamRegistry)
SqliteAgentRunStore          → IAgentRunStore                   (Phase 52)
SqliteEvalResultStore  →  IEvalResultStore                      (Phase 42.5, replaced file-based)
```

Storage is initialized during `InitializeRuntimeAsync` (called from `Program.cs:154` in the server, after `app.Build()`) — migrations run before MCP servers connect or any request is served. The flow on every boot is:

```
CreateConnection → SetPragmas → MigrationRunner.RunPendingMigrations
                                 ↓
                        SeedDefaultUser (INSERT OR IGNORE)
                                 ↓
                        SeedPersonalWorkspace (INSERT OR IGNORE)
```

Both seeders are idempotent — they're safe to run on every boot, and they backfill missing rows (e.g., a pre-Phase-35 user row that has no personal workspace will get one auto-created on the first boot of Phase-35-aware code).

---

## Environment Variables

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_DB_PATH` | `~/.sovrant/data/sovrant.db` | SQLite database file path |
| `SOVRANT_USER_ID` | OS username | User identity for session ownership and audit |
| `SOVRANT_SESSION_JSONL` | `false` | Also write sessions to JSONL (dual-write) |
| `SOVRANT_AUDIT_JSONL` | `false` | Also write audit events to JSONL (dual-write) |
| `SOVRANT_DB_REQUIRE` | `false` | Phase 42.5. When `true`/`1`/`yes`, `InitializeAsync` throws on any init failure (bad path, unwritable dir, corrupt file) instead of logging ERROR and continuing with no persistence. Recommended in production so a broken install fails fast at boot. |
| `SOVRANT_DB_BACKUP_ON_UPGRADE` | `false` | Phase 42.5. When `true`/`1`/`yes`, `InitializeAsync` checkpoints the WAL and copies `sovrant.db` to `sovrant.db.bak-{currentVersion}` before running any pending migrations. A backup is only taken when there are pending migrations *and* the DB is not a fresh install (`currentVersion > 0`). If the copy itself fails, migration is aborted with `InvalidOperationException`. |

---

## Error Handling

- **Directory creation failure** — logged at ERROR level, app continues (unless `SOVRANT_DB_REQUIRE=true`)
- **Database open/migration failure** — logged at ERROR level, `SchemaVersion` stays at 0, app continues (unless `SOVRANT_DB_REQUIRE=true`)
- **Migration checksum drift** — always thrown as `MigrationDriftException`, regardless of `SOVRANT_DB_REQUIRE`
- **Individual store operations** — propagate exceptions to callers (the agentic loop, server endpoints)

The design prioritizes availability over consistency by default: if the database can't be created, the engine still runs — session history and audit are lost for that run, but the core agentic loop functions normally. Production deployments should flip this trade-off with `SOVRANT_DB_REQUIRE=true`.

---

## Security

| Concern | Mitigation |
|---|---|
| SQL injection | All queries use parameterized `$name` parameters, never string interpolation |
| Credential at rest | AES-256-GCM encryption with per-credential random nonces; master key in separate `.keystore` file |
| File permissions | Database at `~/.sovrant/data/` inherits user-profile directory permissions; `.keystore` set to `600` on POSIX |
| Concurrent access | WAL mode + `busy_timeout=5000` allows CLI and server to safely share the same database file |
| Server auth | All HTTP endpoints require `Authorization: Bearer <SOVRANT_TOKEN>`; database is never directly exposed |

---

## What Stays as Files

Not everything moved to SQLite. These remain file-based by design:

| Resource | Location | Reason |
|---|---|---|
| Agent templates | `.sovrant/agents/templates/*.md` | Markdown content, version-controlled with project |
| Skills | `.sovrant/skills/*.md` | Same as templates |
| Memory bootstrap | `~/.sovrant/memory.md`, `.sovrant/memory.md` | Human-editable, injected into system prompt |
| App config | `.sovrant/settings.json`, `hooks.json`, `governance.json` | File merge + env vars, human-editable |
| Rolling logs | `~/.sovrant/logs/` | Append-only text files, rotated daily |
| Master key | `~/.sovrant/credentials/.keystore` | Separate from DB for defense-in-depth |
| Temp scripts | `~/.sovrant/scripts/` | Short-lived, cleaned up automatically |
| Eval definitions | `.sovrant/evals/*.json` | Human-authored, version-controlled |
| Eval results | `~/.sovrant/evals/results/` | File-based (SQLite table ready for future migration) |

---

## Known Concerns & Future Work

The following concerns were surfaced during the Phase 37 audit of the SQLite layer. Items marked **✓ Resolved in Phase 42.5** have landed; the remainder are deferred.

> **Real-world V005 → V008 upgrade walkthrough, captured 2026-04-07 from `~/.sovrant/data/sovrant.db` on a developer workstation:**
>
> **Before** (binary predated V006):
> - `schema_version` rows: 5 (V001–V005 only).
> - `workspace_memory` table: absent. All `_workspace`/`_project` indexes from V006/V007: absent.
> - `users`: 1 (`eramseur`). `workspaces`: 0. `sessions`: 34 (all `workspace_id` NULL). `session_entries`: 132. `audit_bash`: 10.
>
> **After running a V008-aware binary once** (server boot is enough — `InitializeAsync` runs on every startup):
> - `schema_version` rows: **8** (V006, V007, V008 all applied in one boot).
> - `workspace_memory` table: present. 19 new workspace/project indexes added.
> - `SeedPersonalWorkspace` ran with `INSERT OR IGNORE` and created `ws-personal-eramseur` plus the matching `workspace_members` row.
> - V008 backfilled `sessions.workspace_id` for the 34 orphan rows (and `audit_bash` via the `session_id` join).
>
> **What this proves:**
> 1. Additive migrations work — the legacy DB serves reads/writes against V005 tables with no errors right up until the upgrade.
> 2. The upgrade requires zero manual intervention. Booting any V008-aware binary applies V006 + V007 + V008 in order and seeds the personal workspace.
> 3. V008 only backfills when a personal workspace exists for the row's user, so multi-user installs are safe — users without one are left untouched until their personal workspace is created.
> 4. As of Phase 42.5 the CLI now has `sovrant db migrate` (and `--dry-run`), `sovrant db status`, `sovrant db version`, `sovrant db backup`, and `sovrant db inspect` — all call through `IStorageProvider.InitializeAsync` directly so migrations apply without needing to boot the full server. The pre-42.5 gap where the CLI `status` subcommand didn't touch migrations is closed.
> 5. Operators can query the running schema version any time with `sovrant db version`, or get a full row-count breakdown with `sovrant db status`. The same information is also exposed to unauthenticated monitors via `GET /health`'s new `db` block.

| # | Concern | Why it matters | Status |
|---|---|---|---|
| 1 | **Parallel JSONL persistence is still wired in.** `SOVRANT_SESSION_JSONL` and `SOVRANT_AUDIT_JSONL` dual-write to flat files. | Two stores of truth drift; consumers don't know which is canonical. | Deferred — consolidate into SQLite as the sole source; keep dual-write only as a one-shot migration tool. |
| 2 | **Silent init failures.** A bad `SOVRANT_DB_PATH`, an unwritable home directory, or a permissions error logs ERROR and continues. | Production installs can run for hours with zero persistence and nobody notices. | **✓ Resolved in Phase 42.5.** `SOVRANT_DB_REQUIRE=true` makes `InitializeAsync` rethrow any init failure as `InvalidOperationException`. |
| 3 | **No CLI introspection.** There was no `sovrant db status / migrate / backup` subcommand. | Users had no supported way to see schema version, force a migration, or take a backup before upgrades. | **✓ Resolved in Phase 42.5.** `sovrant db status`, `sovrant db version`, `sovrant db migrate [--dry-run]`, `sovrant db backup [path]`, and `sovrant db inspect <table> [--limit N]` all live next to `sovrant db import-swarm`. |
| 4 | **`SOVRANT_DB_PATH` is undocumented in user-facing docs.** It is honored by code but only mentioned in this doc. | Power users can't easily relocate the DB to a network share or alternate disk. | Partially addressed — documented in this file and in `sovrant db` help. README coverage is still pending. |
| 5 | **No backup-before-migrate.** The migration runner applied V006/V007 in place, with no snapshot of the prior file. | A migration bug or a power failure mid-migration leaves the DB in an undefined state with no rollback. | **✓ Resolved in Phase 42.5.** `SOVRANT_DB_BACKUP_ON_UPGRADE=true` checkpoints the WAL and copies the DB to `{path}.bak-{currentVersion}` before applying pending migrations. Failure to back up aborts the migration rather than proceeding without a snapshot. See [DB Upgrades](#db-upgrades-phase-425). |
| 6 | **Migration checksum drift is recorded but not enforced.** `schema_version` stored SHA-256 of each script, but the runner did not raise on mismatch. | A patched migration silently shipping to users would pass checks. | **✓ Resolved in Phase 42.5.** `MigrationRunner.VerifyNoChecksumDrift` runs before applying new migrations and throws `MigrationDriftException` on any mismatch. Null legacy checksums are tolerated so existing installs upgrade cleanly. |
| 8 | **Empty `user_id` defaults.** Sessions, audits, and token usage were written with `user_id = ''` or `Environment.UserName`. | API-created users (Phase 37) had no derived stats until per-user identity propagated through the agentic loop. | **✓ Resolved in Phase 38 (identity plumbing) + Phase 42.5 V009 (data backfill).** V009 backfills any pre-existing empty-string rows to the oldest active admin user. |
| 9 | **No shared bootstrap helper.** `SqliteStorageProvider.InitializeAsync` is called once, but other code paths (test fixtures, future CLI tools) re-implement parts of the boot flow. | Drift between server boot and test boot can mask bugs. | Deferred — extract a `SovrantStorageBootstrap.InitializeAsync` shared helper. |
| 10 | **Connection-per-call with no pool.** Every store opens a fresh `SqliteConnection`. WAL + connection cache helps, but there is no explicit pool. | Under load (server + parallel CLI), we may starve on file handles. | Deferred — evaluate `Microsoft.Data.Sqlite` connection pooling; benchmark before/after. |
| 11 | **No `sovrant init` first-boot UX.** A user installing fresh has no clear "the DB is ready" feedback. | Discovery is poor; failures are invisible. | Partially addressed — `sovrant db status` now prints path, schema version, and row counts after a one-shot init. A top-level `sovrant init` wrapper is still pending. |
| 12 | **Health-check coverage is partial.** `/health` did not exercise the DB at all. | A DB that is read-only (disk full, permissions) reported healthy. | **✓ Resolved in Phase 42.5.** `/health` now calls `IStorageProvider.CheckHealth()` and returns a `db` block with `status`, `schema_version`, `path`, and optional `error`. A failing probe flips the overall status to `degraded` while still returning HTTP 200. A write canary remains a future refinement. |

---

## Testing

The persistence layer is exercised by the full solution test suite (**1,689 tests** across 10 projects, all green as of 2026-05-09). Storage-focused suites include:

| Test Class | Validates |
|---|---|
| `SqliteStorageProviderTests` | DB creation, schema version, idempotent init, transactions, graceful error handling |
| `SqliteSessionStoreTests` | Append/load round-trip, ordering, optional fields, null handling, list sessions |
| `SqliteMemoryStoreTests` | Summaries, patterns, instincts, reinforcement, correction, pruning |
| `SqliteAuditStoreTests` | Governance events, bash commands, batch writes |
| `SqliteTokenUsageStoreTests` | Record/aggregate, empty session, cost tracking |
| `MigrationRunnerTests` | All migrations apply in order, idempotency, expected tables present, V008 backfills only for users with a personal workspace |
| `SqliteWorkspaceStoreTests` | Workspace CRUD, personal-workspace idempotency, members, invites, config, memory, usage aggregation |
| `SqliteProjectStoreTests` | Project CRUD, archive/unarchive, open-by-default access, member roles, 3-tier config inheritance, merged memory views |
| `SqliteUserStoreTests` | Server-generated IDs, validation (username/email/role), duplicate detection, list filters, profile derived stats, soft-delete idempotency, FK preservation, usage aggregation, mass-assignment safety |

All server integration tests use isolated in-memory SQLite databases (unique per test factory instance via `Cache=Shared` named memory DBs). File-backed store tests use temp files cleaned up via `IAsyncDisposable`.

---

## Disk Layout

After a fresh install and first run, `~/.sovrant/` contains:

```
~/.sovrant/
├── data/
│   └── sovrant.db          ← SQLite database — sessions, memory, audit, token_usage,
│                             credentials, workspaces, projects, users, RBAC tables
├── credentials/
│   └── .keystore            ← AES-256-GCM master key (hex, 600 on POSIX)
├── logs/
│   └── sovrant-2026-04-06.log
├── memory.md                ← Global memory (human-edited)
├── sessions/                ← (legacy, only if SOVRANT_SESSION_JSONL=true)
├── audit/                   ← (legacy, only if SOVRANT_AUDIT_JSONL=true)
├── swarm/sessions/          ← (legacy, pre-Phase 37.5; import via `sovrant db import-swarm`)
└── evals/results/           ← Legacy eval report JSON files (migrated to `eval_results` table)
```

A fresh boot with no existing DB produces:
- `data/sovrant.db` at schema version 16 (V001–V016 applied in order)
- A `users` row for the OS username (or `SOVRANT_USER_ID`) inserted via `SeedDefaultUser`
- A `workspaces` row `ws-personal-{userId}` inserted via `SeedPersonalWorkspace`
- A `workspace_members` row linking the seeded user as `owner`
