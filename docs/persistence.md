# Sovrant — Persistence Layer

**Phases 32–42.5, 51, 52, 55, 57, 78, 85, 87, 88, 90, 93, 98, 108–116, 123–126** | **Last updated:** 2026-06-25 | **Current schema:** V043

This document describes how Sovrant stores durable operational data. All persistent state (sessions, memory, audit, credentials, token usage, workspaces, projects, users, knowledge, hooks, MCP/LSP config) is managed by a relational database. Three deployment modes are supported:

- **SQLite** (default) — single-file, zero infrastructure, runs anywhere.
- **Standalone PostgreSQL** — same auth model as SQLite, drop-in for production or team deployments that want a real DB server.
- **Supabase** — PostgreSQL hosted by Supabase, adds GoTrue (SSO-capable auth), JWT-based sessions, and optional Row Level Security.

The SQLite schema described in this document is the master reference; PostgreSQL (both standalone and Supabase) mirrors the same logical schema. Flat-file stores (JSONL, JSON) remain available as a dual-write legacy option but the database is the sole source of truth for all new features.

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
   ┌──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┬──────┐
   ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼      ▼
ISession IMemory IAudit IToken ICredl IWork-  IProject IUser  Eval  Swarm  Runtime Mission
Store    Store   Store  Usage  Store  space   Service  Service Store Event  Trace   Store
                        Store         Service                  Store Store
                  ITeam  IAgent IKnow-  IHook  IWorkspace IMcp   IKey
                  Registry Run   ledge  Store  Settings  Trust  Store
                         Store   Store         Store     Store
```

Every domain store receives an `ISqliteConnectionFactory` via constructor injection, creates a new connection per operation, and uses parameterized queries exclusively. The provider is registered once as a singleton.

---

## Database Location

| Scenario | Path |
|---|---|
| Default (CLI + server) | `~/.sovrant/data/sovrant.db` |
| Custom | Set `SOVRANT_DB_PATH` environment variable |
| Tests | Temp file (`%TEMP%/sovrant_test_{guid}.db`, deleted on test dispose) |

Resolution order:
1. Explicit `dbPath` argument (used by tests).
2. `SOVRANT_DB_PATH` environment variable.
3. Default: `{UserProfile}/.sovrant/data/sovrant.db`.

The data directory is created automatically on first run. If the directory cannot be created or the database cannot be opened, Sovrant logs an `ERROR` and continues — no crash on a bad path. **This graceful degradation is by design but can mask broken installs.** Production installs should set `SOVRANT_DB_REQUIRE=true` so init failures throw `InvalidOperationException` at boot instead.

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
| V006 | `V006__workspaces.sql` | `workspace_memory`; workspace/project indexes on sessions, token_usage, session_summaries, audit, swarm, eval tables |
| V007 | `V007__projects.sql` | Project-scoped indexes on sessions, token_usage, workspace_memory, project_members |
| V008 | `V008__backfill_orphan_workspaces.sql` | One-time backfill: sets `workspace_id` on orphan sessions/audit rows to `ws-personal-{user_id}` where that workspace exists |
| V009 | `V009__backfill_empty_user_ids.sql` | One-time backfill: fills `user_id = ''` rows in sessions/token_usage/credentials with the oldest active admin |
| V010 | `V010__runtime_traces.sql` | `runtime_traces` (IExecutor state log), `mission_scratchpad` (shared store for parallel sub-agents) |
| V011 | `V011__missions.sql` | `missions`, `mission_events` |
| V012 | `V012__unified_orchestration.sql` | `teams`, `team_members`, `agent_runs`; extends `swarm_events` with `kind` + `run_id` |
| V013 | `V013__coordination_mailbox.sql` | `coordination_events`, `group_pm_assignments` |
| V014 | `V014__session_titles.sql` | `sessions.title` column + partial index |
| V015 | `V015__teams_run_profile.sql` | Six run-profile columns on `teams`: `run_mode`, `max_concurrent`, `file_locks_enabled`, `quality_gate_enabled`, `quality_gate_threshold`, `decomposition_mode` |
| V016 | `V016__session_entry_provider.sql` | `session_entries.provider` column for "Provider · Model" display on loaded chats |
| V017 | `V017__hooks.sql` | `hooks` table — replaces `.sovrant/hooks.json`; event/command/matcher/timeout per row |
| V018 | `V018__workspace_settings.sql` | `workspace_settings` table — workspace-scoped budgets, TTL, runtime knobs; `workspace_id=''` = global default |
| V019 | `V019__mcp_lsp_servers.sql` | `mcp_servers`, `lsp_servers` — replaces `settings.json` entries; secrets stay in credential store |
| V020 | `V020__user_preferences.sql` | `user_preferences` — replaces `~/.sovrant/settings.json` per-user fields (Model, Provider, PermissionMode, etc.) |
| V021 | `V021__provider_profiles.sql` | `provider_profiles` — saved provider configurations; API keys stored as `credential_id` refs, never plaintext |
| V022 | `V022__workspace_identity_unification.sql` | Data backfill: normalizes legacy `personal` sentinel to `ws-personal-{user_id}` format |
| V023 | `V023__mcp_http_transport.sql` | Adds `url` + `headers_json` to `mcp_servers` for HTTP transport (stdio = url IS NULL) |
| V024 | `V024__session_mcp_connections.sql` | Adds `mcp_servers` JSON column to `sessions` for per-session MCP gating |
| V025 | `V025__swarm_events_user_id.sql` | Adds `user_id` to `swarm_events` for ownership verification |
| V026 | `V026__auth_credentials.sql` | Adds `password_hash` to `users`, `last_used_at` to `api_tokens`; creates `server_settings` and `password_reset_tokens` |
| V027 | `V027__workspace_provider_profiles.sql` | Adds `workspace_id` to `provider_profiles` for shared workspace-scoped provider config (admin-only write; workspace member read) |
| V028 | `V028__agent_run_prompt.sql` | Adds `prompt` column to `agent_runs` for Recent Runs display |
| V029 | `V029__swarm_federation.sql` | Adds `parent_swarm_id` to `swarm_events` for child-swarm tracking in federated modes |
| V030 | `V030__activity_is_private.sql` | Adds nullable `is_private` (default 0) to `missions`, `agent_runs`, `sessions` for per-record privacy in Command Center / User Dashboard |
| V031 | `V031__session_agent_name.sql` | Adds `sessions.agent_name` column so the bound agent is restored on session resume |
| V032 | `V032__mcp_trust_rules.sql` | `mcp_trust_rules` — per-workspace glob-pattern trust/block rules for MCP tool calls |
| V033 | `V033__knowledge_pages.sql` | `knowledge_pages` — DB-backed store for skills, agents, documents, tools; `tier` = BuiltIn or User; `workspace_id=''` = global |
| V034 | `V034__knowledge_agent_columns.sql` | Adds `role` + `recommended_level` nullable columns to `knowledge_pages` for agent-kind rows |
| V035 | `V035__seed_builtin_knowledge.sql` | Seed data only — inserts built-in skills and agent templates as `tier='BuiltIn'` rows |
| V036 | `V036__document_template_columns.sql` | Adds `fields_json` + `filename_template` to `knowledge_pages` for document-template kind |
| V037 | `V037__seed_builtin_document_templates.sql` | Seed data only — inserts 42 built-in document templates as `tier='BuiltIn'` rows |
| V038 | `V038__knowledge_attributions.sql` | `knowledge_attributions` — records which knowledge items (skills, agents, templates) were invoked per session turn for provenance |
| V039 | `V039__keystore.sql` | `keystore` table — migrates AES-256-GCM master key from `~/.sovrant/credentials/.keystore` file into the DB. The file is read, inserted, and deleted on first boot; new installs never create the file. |
| V040 | `V040__mcp_server_id.sql` | Adds stable `id` column to `mcp_servers` (UUID surrogate that survives renames; `name` remains the routing key) |
| V041 | `V041__workspace_memory_privacy.sql` | Adds `owner_user_id` + `is_private` to `workspace_memory` for per-user note privacy |
| V042 | `V042__memory_owner_user_id.sql` | Adds `owner_user_id` to `session_summaries`, `learned_patterns`, `instincts` so auto-generated memories are scoped to their session owner |
| V043 | `V043__email_as_user_id.sql` | Rewrites `usr_{hex}` primary keys to email addresses; drops `username` column via table recreation |

V008, V009, V022, V035, V037 ship no new tables — they are data backfills or seed inserts. V014–V016, V023–V025, V027–V031, V034, V036, V040–V043 add only columns to existing tables.

Migrations are idempotent — running `InitializeAsync` multiple times is safe. The runner skips already-applied versions and records the SHA-256 checksum of each script in `schema_version.checksum`. Checksum drift is enforced: if a previously-applied `V00X__*.sql` file has been edited in place, `InitializeAsync` throws `MigrationDriftException` on the next boot. Legacy rows with `checksum = NULL` are tolerated so pre-42.5 installs upgrade cleanly.

---

## DB Upgrades (Phase 42.5)

### When migrations run

`InitializeAsync` is invoked exactly once per process boot. The flow is:

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

Any failure at the migration step is caught and logged. With `SOVRANT_DB_REQUIRE=true` the failure is rethrown; without the flag the engine continues with `SchemaVersion = 0`.  `MigrationDriftException` is always rethrown regardless of `SOVRANT_DB_REQUIRE`.

### Recommended first-boot and upgrade procedure

1. Set `SOVRANT_DB_BACKUP_ON_UPGRADE=true` **and** `SOVRANT_DB_REQUIRE=true` before upgrading production.
2. Run `sovrant db migrate --dry-run` to see which versions would apply.
3. Run `sovrant db status` to record baseline row counts.
4. Boot the new binary once (`sovrant db migrate` or a server start is enough).
5. Verify with `sovrant db status` and `sovrant db version`. Restore from backup if row counts regressed.

### Rolling back

Rollback is always a file-level operation — SQLite migrations are forward-only. The backup lives next to the main DB as `sovrant.db.bak-{previousVersion}`:

```bash
# Stop every process using the DB first.
cp ~/.sovrant/data/sovrant.db.bak-41  ~/.sovrant/data/sovrant.db
rm ~/.sovrant/data/sovrant.db-wal     2>/dev/null
rm ~/.sovrant/data/sovrant.db-shm     2>/dev/null
```

Delete the WAL/SHM sidecars — they belong to the post-migration DB.

### Migration drift

`schema_version.checksum` carries the SHA-256 of the migration SQL at the time it was applied. A mismatch throws `MigrationDriftException`.

- **Unintentional drift**: revert the edit to match the stored checksum.
- **Intentional correction**: add a new V-numbered migration. Never edit shipped migrations.
- **Last resort (dev only)**: manually update `schema_version.checksum` via `sqlite3` and document why.

### Health endpoint

`GET /health` includes a `db` block:

```json
{
  "status": "ok",
  "db": {
    "status": "ok",
    "schema_version": 42,
    "path": "/home/user/.sovrant/data/sovrant.db",
    "error": null
  }
}
```

A failing probe flips `db.status` to `"error"` and overall `status` to `"degraded"` while still returning HTTP 200.

---

## Database Inventory (authoritative)

The current schema spans **43 migrations (V001–V043)**. The tables below reflect the schema as of V043.

### Tables by purpose

| Category | Tables | First migration | Notes |
|---|---|---|---|
| **Identity & access** | `users`, `api_tokens`, `roles`, `permissions`, `role_permissions`, `user_roles` | V001 | `password_hash` added V026. `username` column dropped V043 (email is now the PK). `api_tokens` is live (Phase 38). RBAC tables populated (Phase 40). |
| **Auth extras** | `server_settings`, `password_reset_tokens` | V026 | `server_settings`: key/value store for auth bootstrap. `password_reset_tokens`: local-auth only; inert in Supabase mode. |
| **Workspaces** | `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory` | V001 + V006 | `workspace_memory` gained `owner_user_id` + `is_private` (V041) for per-user note privacy. |
| **Projects** | `projects`, `project_members`, `project_config` | V001 | V007 adds indexes only. |
| **Generic config** | `config` | V001 | Scoped key-value (`scope`, `key`, `value`). |
| **Sessions** | `sessions`, `session_entries`, `session_entries_fts` (+ 5 FTS5 internals), `token_usage` | V002 | `sessions` has gained `title` (V014), `mcp_servers` (V024), `is_private` (V030), `agent_name` (V031). FTS via triggers. |
| **Memory** | `session_summaries`, `learned_patterns`, `instincts` | V003 | All three tables gained `owner_user_id` (V042) so auto-generated memories are scoped to the session owner. Filter: `owner_user_id = '' OR owner_user_id = $uid`. |
| **Credentials** | `credentials` | V004 | AES-256-GCM encrypted blobs (nonce + tag + ciphertext). |
| **Keystore** | `keystore` | V039 | Master AES-256-GCM key stored in DB (migrated from `~/.sovrant/credentials/.keystore` file on first V039 boot; file deleted). |
| **Swarm & evals** | `swarm_events`, `eval_runs`, `eval_results` | V005 | `swarm_events` extended with `kind`/`run_id` (V012), `user_id` (V025), `parent_swarm_id` (V029). |
| **Audit** | `audit_governance`, `audit_bash` | V001 | No FK to sessions; workspace/project scoped via nullable TEXT columns. |
| **Engine traces** | `runtime_traces`, `mission_scratchpad` | V010 | Append-only IExecutor state log; shared agent scratchpad. |
| **Missions** | `missions`, `mission_events` | V011 | `missions` gained `is_private` (V030). |
| **Unified orchestration** | `teams`, `team_members`, `agent_runs` | V012 | `teams` run-profile columns added V015. `agent_runs` gained `prompt` (V028), `is_private` (V030). |
| **Inter-agent coordination** | `coordination_events`, `group_pm_assignments` | V013 | PM-to-PM mailbox; workspace-scoped. |
| **Config / settings** | `hooks`, `workspace_settings`, `user_preferences`, `provider_profiles`, `server_settings` | V017–V021, V026 | Replaced on-disk JSON config files. `provider_profiles` got workspace-scoped sharing V027. |
| **MCP / LSP** | `mcp_servers`, `lsp_servers` | V019 | `mcp_servers` HTTP transport (V023), stable `id` column (V040). |
| **MCP trust rules** | `mcp_trust_rules` | V032 | Per-workspace glob rules for MCP tool call trust/block. |
| **Knowledge** | `knowledge_pages` | V033 | Skills, agents, documents, document-templates. `tier='BuiltIn'` rows seeded V035+V037. Agent columns V034, document-template columns V036. |
| **Knowledge provenance** | `knowledge_attributions` | V038 | Per-turn record of which skills/agents/templates were invoked. |
| **Migration metadata** | `schema_version` | bootstrapped by `MigrationRunner` | Version, `applied_at`, SHA-256 `checksum`. |

### Foreign-key topology

```
users ──┬── workspaces.owner_id           (RESTRICT)
        ├── workspace_members.user_id     (RESTRICT)
        ├── project_members.user_id       (RESTRICT)
        ├── api_tokens.user_id            (RESTRICT)
        ├── user_roles.user_id            (RESTRICT)
        └── password_reset_tokens.user_id (CASCADE on delete)

workspaces ──┬── workspace_members.workspace_id
             ├── workspace_config.workspace_id
             ├── workspace_invites.workspace_id
             ├── workspace_memory.workspace_id    (CASCADE on delete)
             └── projects.workspace_id            (nullable)

projects ──┬── project_members.project_id
           └── project_config.project_id

sessions ──── session_entries.session_id          (CASCADE on delete)
eval_runs ──── eval_results.run_id                (CASCADE on delete)
teams ──── team_members.team_id                   (CASCADE on delete)
```

`sessions`, `token_usage`, `audit_governance`, `audit_bash`, `credentials`, `session_summaries`, `swarm_events`, and `eval_runs` all carry `workspace_id` / `project_id` as **nullable, unconstrained TEXT** columns — no FK, so legacy rows from before workspaces existed remain valid.

`owner_user_id` columns on `workspace_memory`, `session_summaries`, `learned_patterns`, and `instincts` are also unconstrained TEXT — `''` means unowned/legacy (visible to all users via the `OR` filter), non-empty means scoped to that user.

### Triggers

| Trigger | Table | Purpose |
|---|---|---|
| `session_entries_ai` | `session_entries` | After insert: mirror into FTS5 |
| `session_entries_ad` | `session_entries` | After delete: tombstone FTS row |
| `session_entries_au` | `session_entries` | After update: tombstone old + insert new |

These three are the entire trigger set. All `updated_at` columns are written explicitly by application code.

---

## SQLite Configuration

Every connection applies these PRAGMAs:

```sql
PRAGMA journal_mode = WAL;        -- persisted in DB header (one-shot)
PRAGMA synchronous = NORMAL;      -- per-connection
PRAGMA foreign_keys = ON;         -- per-connection
PRAGMA busy_timeout = 5000;       -- per-connection (5s lock wait)
PRAGMA cache_size = -20000;       -- per-connection (20 MB page cache)
```

Only `journal_mode=WAL` persists across connections. All others revert to SQLite defaults for any connection that doesn't run the batch — watch for this when connecting directly with `sqlite3` or third-party tools.

---

## Domain Stores

### Session Store (`SqliteSessionStore`)

| Interface | `ISessionStore` |
|---|---|
| Tables | `sessions`, `session_entries` |
| Features | FTS5 full-text search via `session_entries_fts` (auto-sync triggers); per-session agent binding; MCP server gating; privacy flag |

Operations: `AppendAsync`, `LoadAsync` (session ID + optional ownerUserId), `ListAsync`, `SetTitleAsync`, `SetAgentNameAsync`, `UpdatePrivacyAsync`, `SetMcpConnectionsAsync`.

**Dual-write:** Set `SOVRANT_SESSION_JSONL=true` to also write legacy `~/.sovrant/sessions/{id}.jsonl` files.

### Memory Store (`SqliteMemoryStore`)

| Interface | `IMemoryStore` |
|---|---|
| Tables | `session_summaries`, `learned_patterns`, `instincts` |
| Features | Three-layer auto-generated memory; `owner_user_id` privacy scoping (V042) |

Three layers:
1. **Session summaries** — condensed records of past sessions, scoped by project path + owner
2. **Learned patterns** — project conventions with confidence scoring (saved via `/remember`)
3. **Instincts** — trigger/action pairs with confidence decay, reinforcement, pruning

Load methods accept an optional `ownerUserId`: when set the query returns `owner_user_id = '' OR owner_user_id = $uid`. When null (admin/legacy path), all rows are returned. Summaries are generated at session eviction time and stamped with the session owner.

### Workspace Service (`SqliteWorkspaceStore`)

| Interface | `IWorkspaceService` |
|---|---|
| Tables | `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `workspace_memory` |
| Features | Personal workspace auto-create; team workspaces; invite tokens; `workspace_memory` with per-user privacy (V041) |

`workspace_memory` note privacy: `ListMemoryAsync` accepts `viewerUserId` — when set, returns `is_private = 0 OR owner_user_id = $uid`. When null (admin), returns all rows.

### User Service (`SqliteUserStore`)

| Interface | `IUserService` |
|---|---|
| Tables | `users` (read/write); `sessions`, `token_usage`, `audit_governance` (read-only joins) |
| Features | Server-generated `usr_{16hex}` IDs; soft-delete only; strict validation; per-user profile with derived stats; per-user usage with model + date filters |

`users.password_hash` is populated for local-auth deployments; NULL for Supabase Auth users.

### Token Service (`SqliteTokenService`)

| Interface | `ITokenService` |
|---|---|
| Tables | `api_tokens` |
| Features | `svt_` prefixed bearer tokens; SHA-256 hashed storage; `last_used_at` sliding TTL; scopes (stored, not enforced) |

CLI and API callers authenticate with `svt_*` tokens in all deployment modes (SQLite, standalone Postgres, Supabase). In Supabase mode, interactive UI sessions use JWTs instead, but machine access continues via `svt_*`.

### Credential Store (`SqliteCredentialStore`)

| Interface | `ICredentialStore` |
|---|---|
| Tables | `credentials`, `keystore` |
| Encryption | AES-256-GCM per-credential, random nonces |

As of V039, the master AES-256-GCM key is stored in the `keystore` table (scope = `'default'`). On the first boot of V039-aware code, any existing `~/.sovrant/credentials/.keystore` file is read, inserted into the table, and deleted. New installs never create the file.

### Audit Store (`SqliteAuditStore`)

| Interface | `IAuditStore` |
|---|---|
| Tables | `audit_governance`, `audit_bash` |

Governance events (tool name, phase, action, rule, reason) and bash commands (text, exit code).

**Dual-write:** Set `SOVRANT_AUDIT_JSONL=true` to also write legacy JSONL files.

### Token Usage Store (`SqliteTokenUsageStore`)

| Interface | `ITokenUsageStore` |
|---|---|
| Tables | `token_usage` |

Per-turn records with model, input/output tokens, optional cost. `GetSessionTotalsAsync` aggregates for a session.

### Knowledge Store (`SqliteKnowledgeStore`)

| Interface | `IKnowledgeStore` |
|---|---|
| Tables | `knowledge_pages`, `knowledge_attributions` |
| Features | Skills, agents, documents, document-templates; BuiltIn vs User tiers; per-turn attribution tracking; copy-on-write for BuiltIn edits |

`tier='BuiltIn'` rows are seeded by V035 and V037. User edits on built-in items create a `tier='User'` overlay row (`workspace_id` set to the user's workspace); the original BuiltIn row is never modified. Per-turn attribution (`knowledge_attributions`) records which knowledge items were injected.

### Config / Settings Stores

| Store | Interface | Table |
|---|---|---|
| `SqliteHooksStore` | `IHooksStore` | `hooks` |
| `SqliteWorkspaceSettingsStore` | `IWorkspaceSettingsStore` | `workspace_settings` |
| `SqliteUserPreferencesStore` | `IUserPreferencesStore` | `user_preferences` |
| `SqliteProviderProfilesStore` | `IProviderProfilesStore` | `provider_profiles` |

These replaced the on-disk `hooks.json`, `settings.json`, and provider config files that existed before V017–V021.

### MCP / Trust Stores

| Store | Interface | Table |
|---|---|---|
| `SqliteMcpServerStore` | `IMcpServerStore` | `mcp_servers`, `lsp_servers` |
| `SqliteMcpTrustRuleStore` | `IMcpTrustRuleStore` | `mcp_trust_rules` |

`mcp_servers` supports both stdio (`url IS NULL`) and HTTP transports. The stable `id` column (V040) allows workspace-scoped gating rules to survive server renames. Trust rules use glob patterns for tool names.

### Orchestration / Eval Stores

| Store | Table |
|---|---|
| `SqliteSwarmEventStore` | `swarm_events` |
| `SqliteEvalResultStore` | `eval_runs`, `eval_results` |
| `SqliteRuntimeTraceStore` | `runtime_traces` |
| `SqliteMissionScratchpadStore` | `mission_scratchpad` |
| `SqliteMissionStore` | `missions`, `mission_events` |
| `SqliteTeamRegistry` | `teams`, `team_members` |
| `SqliteAgentRunStore` | `agent_runs` |

---

## Dependency Injection

All stores are registered as singletons in `ServiceCollectionExtensions.AddSovrantRuntime()`:

```
SqliteStorageProvider        →  IStorageProvider + ISqliteConnectionFactory
SqliteSessionStore           →  ISessionStore      (or DualWriteSessionStore)
SqliteMemoryStore            →  IMemoryStore
SqliteAuditStore             →  IAuditStore        (or DualWriteAuditStore)
SqliteTokenUsageStore        →  ITokenUsageStore
SqliteCredentialStore        →  ICredentialStore
SqliteTokenService           →  ITokenService
SqliteWorkspaceStore         →  IWorkspaceService
SqliteProjectStore           →  IProjectService
SqliteUserStore              →  IUserService
SqliteSwarmEventStore        →  ISwarmEventStore
SqliteRuntimeTraceStore      →  IRuntimeTraceStore
SqliteMissionScratchpadStore →  IMissionScratchpadStore
SqliteMissionStore           →  IMissionStore
SqliteTeamRegistry           →  ITeamRegistry
SqliteAgentRunStore          →  IAgentRunStore
SqliteEvalResultStore        →  IEvalResultStore
SqliteKnowledgeStore         →  IKnowledgeStore
SqliteHooksStore             →  IHooksStore
SqliteWorkspaceSettingsStore →  IWorkspaceSettingsStore
SqliteUserPreferencesStore   →  IUserPreferencesStore
SqliteProviderProfilesStore  →  IProviderProfilesStore
SqliteMcpServerStore         →  IMcpServerStore
SqliteMcpTrustRuleStore      →  IMcpTrustRuleStore
```

Storage is initialized during `InitializeRuntimeAsync` (called from `Program.cs` after `app.Build()`) — migrations run before MCP servers connect or any request is served.

---

## Deployment Modes and PostgreSQL / Supabase

Sovrant supports three storage deployment modes. The SQLite schema described above is the master reference. PostgreSQL (both standalone and Supabase) mirrors the same logical schema.

### The three modes

| | SQLite | Standalone PostgreSQL | Supabase |
|---|---|---|---|
| User creation | app (`/auth/register`, admin) | same as SQLite | mirror trigger from `auth.users` |
| `users.password_hash` | populated | populated | always NULL (GoTrue handles it) |
| Login flow | app validates password → issues `svt_*` | same as SQLite | Supabase GoTrue → JWT |
| Bearer token type | `svt_*` api_token | `svt_*` api_token | Supabase JWT (UI); `svt_*` (CLI) |
| Token resolution | `SqliteTokenService` | `PostgresTokenService` *(planned)* | JWT JWKS validation + `svt_*` fallback |
| `password_reset_tokens` | used | used | inert (Supabase handles resets) |
| FK tables | reference `public.users` | same | same — mirror trigger ensures row exists first |
| Schema bootstrap | V-series migration runner | `db/postgres/PostgresSchema.sql` (manual `psql`) | `db/supabase/migrations/` via Supabase CLI |
| Row-level security | n/a | optional | Commented-out policies in `db/supabase/migrations/20260625000000_initial_schema.sql` |

### How Supabase Auth fits

Supabase Auth stores identities in `auth.users` (UUID PK, managed by GoTrue). The canonical user identity throughout the app is a `user_id TEXT` column. In Supabase mode this TEXT value is `auth.users.id::TEXT` (a UUID string) — fully compatible with existing FK columns and `owner_user_id` memory columns without any type changes.

Two mirror triggers (in `db/supabase/migrations/20260625000000_initial_schema.sql`) propagate changes from `auth.users` to `public.users`:

```sql
-- on_auth_user_created: fires on new sign-up
-- Role is read from app_metadata.sovrant_role (service-role only — users cannot self-set).
-- Whitelist: 'admin' is elevated; anything else → 'user'.
CREATE OR REPLACE FUNCTION public.handle_auth_user_created() ... AS $$
DECLARE _role TEXT;
BEGIN
    _role := CASE
        WHEN NEW.raw_app_meta_data->>'sovrant_role' = 'admin' THEN 'admin'
        ELSE 'user'
    END;
    INSERT INTO public.users (user_id, email, role, ...)
    VALUES (NEW.id::TEXT, NEW.email, _role, ...)
    ON CONFLICT (user_id) DO NOTHING;
END; $$;

-- on_auth_user_updated: fires on email OR raw_app_meta_data changes.
-- When app_metadata changes, re-derives role from sovrant_role whitelist and syncs.
CREATE OR REPLACE FUNCTION public.handle_auth_user_updated() ... AS $$
DECLARE _role TEXT;
BEGIN
    IF NEW.raw_app_meta_data IS DISTINCT FROM OLD.raw_app_meta_data THEN
        _role := CASE WHEN NEW.raw_app_meta_data->>'sovrant_role' = 'admin' THEN 'admin' ELSE 'user' END;
        UPDATE public.users SET email = NEW.email, role = _role, ... WHERE user_id = NEW.id::TEXT;
    ELSE
        UPDATE public.users SET email = NEW.email, ... WHERE user_id = NEW.id::TEXT;
    END IF;
END; $$;
```

This means every FK reference to `users(user_id)` continues to work without schema changes. The app's `GetUserId()` reads from `HttpContext.Items` regardless of how the identity was resolved.

`raw_app_meta_data` is the source of truth for role in Supabase mode — it is writable only by service-role callers (GoTrue enforces this), and the role is embedded in the signed JWT so it can be read without a DB round-trip. Never update `public.users.role` directly in Supabase mode.

### File layout

All database files live under `db/` in the repo root:

```
db/
  postgres/
    PostgresSchema.sql                            ← standalone Postgres (also embedded in Runtime DLL)
  supabase/
    config.toml                                   ← Supabase CLI project config
    migrations/
      20260625000000_initial_schema.sql           ← full schema + GoTrue mirror triggers + RLS stubs
```

**Standalone Postgres** (`db/postgres/PostgresSchema.sql`) — base schema only; no Supabase-specific sections. Run once with `psql` to bootstrap; re-run on upgrades (idempotent). Also embedded in `Sovrant.Runtime.dll` so the runtime can auto-initialize a fresh Postgres database without external files present.

**Supabase** (`db/supabase/migrations/`) — full schema plus the GoTrue mirror triggers (`on_auth_user_created`, `on_auth_user_updated`) and commented-out RLS policies. Managed by the Supabase CLI — run `supabase db push` from `db/supabase/`. Admins can layer their own customizations by adding new numbered migration files after the initial one:

```
db/supabase/migrations/
  20260625000000_initial_schema.sql   ← Sovrant base (don't edit)
  20260625000001_my_org_additions.sql ← admin customizations layered on top
```

The Supabase CLI applies migrations in timestamp order so admin additions never conflict with future Sovrant upgrades as long as they stay additive.

### What changes in auth middleware

`BearerTokenMiddleware` currently handles only `svt_*` tokens. For Supabase mode (activated when `SUPABASE_URL` env var is set), a second path validates Supabase JWTs:

```
token starts with "svt_"  →  ITokenService.ResolveAsync()        (all three modes)
token is a JWT             →  validate against Supabase JWKS      (Supabase mode only)
                               sub claim    = user_id (UUID string)
                               role         = app_metadata.sovrant_role claim in JWT
                                              (no DB query needed — role is in the signed token)
```

`HttpContextAuthExtensions.GetUserId()` and all route handlers are unchanged — they read from `HttpContext.Items` regardless of how the identity was resolved.

Because the role is embedded in the JWT by Supabase (from `raw_app_meta_data`), changing a user's role by updating `app_metadata` takes effect on the next JWT refresh (Supabase access tokens expire every hour by default). For immediate effect after a role change, revoke the user's active session in the Supabase dashboard.

### Pending implementation for Postgres / Supabase

| Item | Status |
|---|---|
| `db/postgres/PostgresSchema.sql` base schema (V001–V043 parity + V040 backfill) | **Done** |
| `db/supabase/migrations/` Supabase CLI migration with GoTrue mirror triggers + RLS stubs | **Done** |
| RLS policies for memory privacy at DB layer | Skeleton commented out in Supabase migration — **Planned** to enable |
| `PostgresTokenService` (mirrors `SqliteTokenService` for standalone Postgres) | **Planned** |
| Postgres V-series migration runner (equivalent to SQLite `MigrationRunner`) | **Planned** |
| JWT validation in `BearerTokenMiddleware` | **Planned** |
| `MutableAuthProvider` (desktop/web) storing Supabase JWT on login | **Planned** |

### Admin bootstrap on Supabase

First user: created via the Supabase dashboard (Auth → Users). The mirror trigger fires automatically, creating the `public.users` row with `role = 'user'`.

**Elevate to admin** by writing `sovrant_role` to the user's `app_metadata` (writable only via service role — users cannot self-elevate). In the Supabase SQL editor:

```sql
UPDATE auth.users
SET    raw_app_meta_data = jsonb_set(
           COALESCE(raw_app_meta_data, '{}'), '{sovrant_role}', '"admin"')
WHERE  email = 'you@example.com';
```

The `on_auth_user_updated` mirror trigger fires on the `raw_app_meta_data` change and syncs `role = 'admin'` to `public.users` automatically. Never write directly to `public.users.role` — `app_metadata` is the source of truth for role in Supabase mode.

**Why `app_metadata` and not `public.users`:** `raw_app_meta_data` is service-role-only (GoTrue enforces this), the role is embedded in the signed JWT so it can be read without a DB query, and Supabase logs all `auth.users` modifications for audit trail. Direct `UPDATE public.users` bypasses all of these.

No Sovrant-specific setup endpoint needed.

---

## Setup Guides

### Standalone PostgreSQL

Prerequisites: PostgreSQL 14+ installed and a database created for Sovrant.

**1. Create the database:**
```sql
CREATE DATABASE sovrant;
CREATE USER sovrant_app WITH PASSWORD 'yourpassword';
GRANT ALL PRIVILEGES ON DATABASE sovrant TO sovrant_app;
```

**2. Set the connection string:**
```bash
export SOVRANT_POSTGRES_URL="postgres://sovrant_app:yourpassword@localhost:5432/sovrant"
```

**3. Bootstrap the schema (one-time, run from the repo):**
```bash
psql $SOVRANT_POSTGRES_URL -f db/postgres/PostgresSchema.sql
```

The standalone schema has no Supabase-specific sections — it is safe to run in full against any plain Postgres instance.

**4. Start Sovrant.** It detects Postgres via `SOVRANT_POSTGRES_URL`. On first boot, `SeedDefaultUser` and `SeedPersonalWorkspace` run automatically (same as SQLite).

**5. Create additional users** via `/auth/register` or the admin API — same as SQLite mode. `password_hash` is populated; `password_reset_tokens` is active.

**Upgrade procedure:** Re-run `PostgresSchema.sql` when Sovrant updates. All table-creation statements use `CREATE TABLE IF NOT EXISTS`; all column additions use `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`. The file is idempotent for column additions but a full Postgres V-series migration runner (parity with the SQLite `MigrationRunner`) is planned — until then, each release's upgrade SQL notes will accompany the release.

---

### Supabase

Prerequisites: Supabase project (cloud at [supabase.com](https://supabase.com) or self-hosted via `supabase start`).

**1. Apply the Sovrant migration via the Supabase CLI (recommended):**
```bash
cd db/supabase
supabase link --project-ref <your-project-ref>
supabase db push
```

Or paste the contents of `db/supabase/migrations/20260625000000_initial_schema.sql` directly into the Supabase SQL Editor. `auth.users` exists in every Supabase project so there are no errors.

**Customizing the schema** — add your own migrations after the initial one:
```
db/supabase/migrations/20260625000001_my_org_additions.sql
```
The CLI applies them in timestamp order. Keep customizations additive so future Sovrant upgrades layer cleanly on top.

**2. (Optional) Enable Row Level Security** — uncomment the RLS block at the end of `20260625000000_initial_schema.sql`. This enforces `owner_user_id` privacy at the database layer (not just application layer), which is the recommended posture for multi-user Supabase deployments. The commented policies are:
- `workspace_memory`: `USING (is_private = 0 OR owner_user_id = auth.uid()::text)`
- `session_summaries`, `learned_patterns`, `instincts`: `USING (owner_user_id = '' OR owner_user_id = auth.uid()::text)`

**3. Set environment variables:**
```bash
export SUPABASE_URL="https://your-project.supabase.co"
export SUPABASE_ANON_KEY="your-anon-key"              # public, safe in client env
export SUPABASE_SERVICE_ROLE_KEY="your-service-key"   # secret — server-side only
```

**4. Start Sovrant.** It detects Supabase mode via `SUPABASE_URL`. User creation and login go through GoTrue, not the Sovrant `/auth/*` routes.

**5. Create users** in the Supabase dashboard → Authentication → Users → "Invite user" or "Create new user". The `on_auth_user_created` mirror trigger fires automatically and creates the `public.users` row with `role = 'user'`. SSO providers (Google, GitHub, etc.) configured in the Supabase dashboard work automatically once the trigger is in place.

**6. Elevate the first admin** (see [Admin bootstrap on Supabase](#admin-bootstrap-on-supabase) above).

**What Supabase mode changes:**
- `password_hash` is always `NULL` — GoTrue owns passwords
- `password_reset_tokens` is inert — Supabase sends reset emails
- `/auth/register` and `/auth/login` routes are bypassed
- Bearer tokens for UI sessions are Supabase JWTs (role embedded as `app_metadata.sovrant_role`)
- CLI and API machine access still uses `svt_*` tokens via `api_tokens`

---

**SQLite (default)**

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_DB_PATH` | `~/.sovrant/data/sovrant.db` | SQLite database file path |
| `SOVRANT_USER_ID` | OS username | User identity for session ownership and audit |
| `SOVRANT_SESSION_JSONL` | `false` | Also write sessions to JSONL (legacy dual-write) |
| `SOVRANT_AUDIT_JSONL` | `false` | Also write audit events to JSONL (legacy dual-write) |
| `SOVRANT_DB_REQUIRE` | `false` | When `true`, `InitializeAsync` throws on any init failure instead of continuing with no persistence. Recommended in production. |
| `SOVRANT_DB_BACKUP_ON_UPGRADE` | `false` | When `true`, checkpoints WAL and copies `sovrant.db` to `sovrant.db.bak-{version}` before any pending migrations run. |

**Standalone PostgreSQL** *(planned — activates when set)*

| Variable | Default | Description |
|---|---|---|
| `SOVRANT_POSTGRES_URL` | *(unset)* | PostgreSQL connection string (e.g. `postgres://user:pass@host:5432/sovrant`). When set, Sovrant uses Postgres instead of SQLite. |

**Supabase** *(activates when `SUPABASE_URL` is set)*

| Variable | Default | Description |
|---|---|---|
| `SUPABASE_URL` | *(unset)* | Supabase project URL (e.g. `https://xxx.supabase.co`). Activates Supabase mode: GoTrue auth, JWT validation in `BearerTokenMiddleware`, Postgres backend. |
| `SUPABASE_ANON_KEY` | *(unset)* | Supabase anon/public key. Used by the web and desktop clients for unauthenticated GoTrue calls (sign-in form). |
| `SUPABASE_SERVICE_ROLE_KEY` | *(unset)* | Supabase service role key. Server-side only — never expose to clients. Required for admin operations and mirror trigger validation. |

---

## Error Handling

- **Directory creation failure** — logged at ERROR, app continues (unless `SOVRANT_DB_REQUIRE=true`)
- **Database open/migration failure** — logged at ERROR, `SchemaVersion` stays at 0, app continues (unless `SOVRANT_DB_REQUIRE=true`)
- **Migration checksum drift** — always thrown as `MigrationDriftException`, regardless of `SOVRANT_DB_REQUIRE`
- **Individual store operations** — propagate exceptions to callers

---

## Security

| Concern | Mitigation |
|---|---|
| SQL injection | All queries use parameterized `$name` parameters; no string interpolation in SQL (static WHERE fragments only for optional filter clauses, always hardcoded strings never user input) |
| Credential at rest | AES-256-GCM encryption; master key in `keystore` table (DB) since V039 |
| Concurrent access | WAL mode + `busy_timeout=5000` allows CLI and server to share the same DB file |
| Server auth | All HTTP endpoints require `Authorization: Bearer`; SQLite DB never directly exposed |
| Memory privacy | `owner_user_id` scoping enforced at app layer (query filter); no DB-level RLS on SQLite. Supabase deployments can enable RLS policies (commented out in `db/supabase/migrations/20260625000000_initial_schema.sql`) to enforce privacy at the DB layer. |
| Role elevation (Supabase) | `raw_app_meta_data.sovrant_role` is writable only by service-role callers (GoTrue enforces); users cannot self-elevate. Role is embedded in the signed JWT — tamper-proof in transit. |

---

## What Stays as Files

| Resource | Location | Reason |
|---|---|---|
| Agent templates | `.sovrant/agents/templates/*.md` | Markdown content, version-controlled with project |
| Skills | `.sovrant/skills/*.md` | Same as templates; DB-backed versions in `knowledge_pages` are the write target for new edits |
| Memory bootstrap | `~/.sovrant/memory.md`, `.sovrant/memory.md` | Human-editable, injected into system prompt at session start |
| Governance rules | `.sovrant/governance.json` | File merge + env vars, human-editable |
| Rolling logs | `~/.sovrant/logs/` | Append-only text files, rotated daily |
| Temp scripts | `~/.sovrant/scripts/` | Short-lived, cleaned up automatically |
| Eval definitions | `.sovrant/evals/*.json` | Human-authored, version-controlled |

**Moved from files to DB:**

| Resource | Moved in | Table |
|---|---|---|
| Hooks (`hooks.json`) | V017 | `hooks` |
| Workspace settings | V018 | `workspace_settings` |
| MCP/LSP server config (`settings.json` entries) | V019 | `mcp_servers`, `lsp_servers` |
| User preferences (`settings.json` per-user fields) | V020 | `user_preferences` |
| Provider profiles (API key references) | V021 | `provider_profiles` |
| Encryption master key (`.keystore` file) | V039 | `keystore` |
| Eval results | V005 (Phase 42.5) | `eval_results` |

---

## Known Concerns & Future Work

> **Real-world V005 → V008 upgrade walkthrough, captured 2026-04-07 from a developer workstation:**
>
> Before (V005): `schema_version` rows: 5. `workspace_memory` absent. `sessions`: 34 (all `workspace_id NULL`).
> After booting V008-aware binary once: V006, V007, V008 applied in one boot. Personal workspace seeded. 34 orphan sessions backfilled. Zero manual intervention required.

| # | Concern | Status |
|---|---|---|
| 1 | **Parallel JSONL persistence still wired.** `SOVRANT_SESSION_JSONL` and `SOVRANT_AUDIT_JSONL` dual-write to flat files. | Deferred — consolidate into SQLite as sole source; keep dual-write only as migration tool. |
| 2 | **Silent init failures.** Bad `SOVRANT_DB_PATH` or permissions error logs ERROR and continues. | **✓ Resolved (Phase 42.5).** `SOVRANT_DB_REQUIRE=true` makes `InitializeAsync` rethrow. |
| 3 | **No CLI introspection.** No `sovrant db` subcommands. | **✓ Resolved (Phase 42.5).** `sovrant db status/version/migrate/backup/inspect` all available. |
| 5 | **No backup-before-migrate.** Migration applied with no snapshot. | **✓ Resolved (Phase 42.5).** `SOVRANT_DB_BACKUP_ON_UPGRADE=true` checkpoints + copies before migrations. |
| 6 | **Migration checksum drift not enforced.** | **✓ Resolved (Phase 42.5).** `MigrationDriftException` thrown on any mismatch. |
| 8 | **Empty `user_id` defaults.** Sessions written with `user_id = ''`. | **✓ Resolved (Phase 38 + V009 backfill).** |
| 13 | **`db/postgres/PostgresSchema.sql` has no migration runner.** One-shot manual script; Supabase instances bootstrapped before V041/V042 silently lack the privacy columns. Existing Postgres instances not re-run against the updated script will have no `owner_user_id` on memory tables → INSERT failures at runtime. | **Planned** — Postgres V-series migration runner needed; or publish discrete upgrade scripts per release (e.g. `V041_upgrade.sql`). |
| 14 | **Memory privacy enforced at app layer only.** `owner_user_id` filter is in application query logic, not DB RLS. Direct Supabase dashboard, service-role queries, or edge functions bypass it entirely. | **Partially done** — RLS policy skeletons are in `db/supabase/migrations/20260625000000_initial_schema.sql` (commented out). Uncomment and run to enforce. Full enforcement requires all Supabase Edge Functions to also use `auth.uid()`. |
| 15 | **Six tables with `REFERENCES users(user_id)` block Supabase Auth adoption.** `workspaces`, `workspace_members`, `project_members`, `api_tokens`, `user_roles`, `password_reset_tokens`. Without the mirror trigger these FK inserts fail because `public.users` has no row. | **✓ Resolved (2026-06-18).** `on_auth_user_created` mirror trigger in the Supabase migration ensures `public.users` row is created before any FK-dependent inserts. |
| 16 | **`/remember` command writes patterns/instincts with `owner_user_id = ''`** making them visible to all users. | **✓ Resolved (2026-06-18).** `ownerUserId` now threaded through `SlashCommandDispatcher.TryDispatchAsync` → `RememberCommand`. |
| 17 | **`GET /workspaces/{id}/memory` returned private entries to any member.** `viewerUserId` was not passed to `ListMemoryAsync`. | **✓ Resolved (2026-06-18).** Route now passes `viewerUserId: ctx.GetUserId()`. |
| 18 | **SQL query fragments built via string interpolation** in `SqliteMemoryStore` and `SqliteWorkspaceStore` for optional owner filters. Currently safe (hardcoded literal strings) but establishes a risky pattern. | Deferred — replace with static tautology queries: `AND ($uid IS NULL OR owner_user_id = '' OR owner_user_id = $uid)`. |
| 19 | **`db/postgres/PostgresSchema.sql` V040 backfill gap.** V040 backfills `mcp_servers.id` with `randomblob(16)` in SQLite. No equivalent UPDATE in the Postgres schema for upgrade installs — existing Postgres rows kept `id=''`. | **✓ Resolved (2026-06-18).** `UPDATE mcp_servers SET id = gen_random_uuid()::text WHERE id = '';` added to the V040 section of both Postgres schema files. |
| 9 | **No shared bootstrap helper.** Test fixtures re-implement parts of the boot flow. | Deferred. |
| 10 | **Connection-per-call with no pool.** | Deferred — benchmark before adding pooling. |
| 11 | **No `sovrant init` first-boot UX.** | Partially addressed — `sovrant db status` covers it. |

---

## Testing

The persistence layer is exercised by the full solution test suite (**2,222 tests** across all projects as of 2026-06-18). Storage-focused suites include:

| Test Class | Validates |
|---|---|
| `SqliteStorageProviderTests` | DB creation, schema version, idempotent init, transactions, graceful error handling |
| `SqliteSessionStoreTests` | Append/load round-trip, ordering, optional fields, null handling, list sessions |
| `SqliteMemoryStoreTests` | Summaries, patterns, instincts, reinforcement, correction, pruning, owner scoping |
| `SqliteAuditStoreTests` | Governance events, bash commands, batch writes |
| `SqliteTokenUsageStoreTests` | Record/aggregate, empty session, cost tracking |
| `MigrationRunnerTests` | All V001–V043 migrations apply in order; idempotency; expected tables present; backfill behavior |
| `SqliteWorkspaceStoreTests` | Workspace CRUD, personal-workspace idempotency, members, invites, config, memory with privacy filtering |
| `SqliteProjectStoreTests` | Project CRUD, archive/unarchive, open-by-default access, member roles, 3-tier config inheritance |
| `SqliteUserStoreTests` | Server-generated IDs, validation, duplicate detection, soft-delete, FK preservation, usage aggregation |

All server integration tests use isolated in-memory SQLite databases (unique per test factory instance via `Cache=Shared` named memory DBs).

---

## Disk Layout

After a fresh install and first run, `~/.sovrant/` contains:

```
~/.sovrant/
├── data/
│   └── sovrant.db          ← SQLite database — all persistent state (V043 schema)
│                             sessions, memory, audit, credentials, keystore,
│                             workspaces, projects, users, knowledge, hooks,
│                             MCP/LSP config, teams, missions, swarm, evals
├── logs/
│   └── sovrant-2026-06-18.log
├── memory.md                ← Global memory (human-edited, injected at session start)
├── sessions/                ← (legacy, only present if SOVRANT_SESSION_JSONL=true)
├── audit/                   ← (legacy, only present if SOVRANT_AUDIT_JSONL=true)
└── swarm/sessions/          ← (legacy, pre-Phase 37.5; import via `sovrant db import-swarm`)
```

Note: `credentials/.keystore` no longer exists on fresh installs (V039 moved the master key into `sovrant.db`'s `keystore` table). Existing installs had the file migrated and deleted on first V039 boot.

A fresh boot with no existing DB produces:
- `data/sovrant.db` at schema version 43 (V001–V043 applied in order)
- A `users` row for `SOVRANT_USER_ID` (or OS username) via `SeedDefaultUser`
- A `workspaces` row `ws-personal-{userId}` via `SeedPersonalWorkspace`
- A `workspace_members` row linking the seeded user as `owner`
- A `keystore` row with a freshly generated AES-256-GCM master key
