# Sovrant — Persistence Layer

**Phases 32, 35, 36, 37, 37.5 (active) — Phase 42.5 planned** | **Last updated:** 2026-04-08

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
ISession IMemory IAudit IToken ICredl IWork-  IProject IUser  Eval   (future)
Store    Store   Store  Usage  Store  space   Service  Service Store SwarmEvent
                        Store         Service
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

The data directory is created automatically on first run. If the directory cannot be created or the database cannot be opened, Sovrant logs an `ERROR` (`"Failed to initialize SQLite storage at {DbPath}. The application will continue but data will not be persisted."`) and continues — no crash on a bad path. **This graceful degradation is by design but can mask broken installs**; see [Known Concerns](#known-concerns--future-work-phase-425) below for the planned `SOVRANT_DB_REQUIRE` opt-in.

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

All V006/V007 statements are **additive** (`CREATE TABLE`, `CREATE INDEX IF NOT EXISTS`), so a database created at V005 or earlier upgrades cleanly on next boot — no manual intervention. V008 then backfills any orphan rows from those upgraded databases.

Migrations are idempotent — running `InitializeAsync` multiple times is safe. The runner skips already-applied versions and records SHA-256 checksums of each script in `schema_version` to detect drift (drift detection itself is a Phase 42.5 item — today the runner records the checksum but does not raise on mismatch).

### Future-proofing

The schema is designed upfront so that Phases 33–37 can ship without `ALTER TABLE`:

- **`workspace_id`** and **`project_id`** existed as nullable columns on sessions, token_usage, audit, credentials, memory, swarm, and eval tables in V001. V006/V007 only added indexes, never columns.
- **`users.role`**, **`users.team`**, **`users.status`** existed in V001. **Phase 37 added the API surface only — no schema changes.**
- **RBAC tables** (`roles`, `permissions`, `role_permissions`, `user_roles`) exist in V001 (still empty). Phase 40 will populate them.
- **`api_tokens`** table exists in V001 (still empty). Phase 38 will populate it.

---

## Database Inventory (authoritative)

The list below is generated from the migration scripts in `src/Sovrant.Runtime/Storage/Migrations/V00*.sql` and was cross-checked against a live `~/.sovrant/data/sovrant.db` on 2026-04-07 after migrations V006–V008 had been applied. After all 8 migrations apply, a fresh database contains **27 application tables** + **1 metadata table** (`schema_version`) + **1 FTS5 virtual table** + **5 FTS5 internal shadow tables** = **34 objects in `sqlite_master`**, plus **26 indexes** and **3 triggers**. V008 ships no schema, only data updates, so it does not change the inventory.

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

26 indexes total at V007. **Notably absent**: there is no index on `users.username` or `users.email` beyond the implicit unique constraint indexes — that is sufficient for lookups since SQLite auto-creates a B-tree for every `UNIQUE` column. There is also no covering index for the per-user audit join (`audit_governance` → `sessions(user_id)`); for now the join is small enough that it's not measurable, but it's listed under Phase 42.5.

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

### Eval Result Store (`IEvalResultStore`)

**Extracted from:** `EvalResultStore` (concrete class)

The existing file-based `EvalResultStore` now implements `IEvalResultStore`. A SQLite-backed implementation can be swapped in later using the `eval_runs` and `eval_results` tables from V005.

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
EvalResultStore        →  IEvalResultStore   (file-based, interface extracted)
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

---

## Error Handling

- **Directory creation failure** — logged at ERROR level, app continues
- **Database open/migration failure** — logged at ERROR level, `SchemaVersion` stays at 0, app continues
- **Individual store operations** — propagate exceptions to callers (the agentic loop, server endpoints)

The design prioritizes availability over consistency: if the database can't be created, the engine still runs — session history and audit are lost for that run, but the core agentic loop functions normally.

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

## Known Concerns & Future Work (Phase 42.5)

The following concerns were surfaced during the Phase 37 audit of the SQLite layer. None are blocking, but all are tracked under **Phase 42.5 — Database Lifecycle: Setup, Upgrade Safety & Introspection** in the roadmap.

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
> 4. The CLI `status` subcommand currently does **not** trigger `InitializeRuntimeAsync`, so it does not run migrations. Use the server (`dotnet run --project src/Sovrant.Server`) or any CLI command that goes through `InitAsync` (e.g. `prompt`). This gap is also tracked under Phase 42.5 (concern #11 — `sovrant init` / `sovrant db migrate`).
> 5. Without a `sovrant db status` CLI, **users still have no way to know which schema version their DB is on**, which is precisely why concerns #2, #3, #11 below exist.

| # | Concern | Why it matters | Planned mitigation |
|---|---|---|---|
| 1 | **Parallel JSONL persistence is still wired in.** `SOVRANT_SESSION_JSONL` and `SOVRANT_AUDIT_JSONL` dual-write to flat files. | Two stores of truth drift; consumers don't know which is canonical. | Consolidate into SQLite as the sole source; keep dual-write only as a one-shot migration tool. |
| 2 | **Silent init failures.** A bad `SOVRANT_DB_PATH`, an unwritable home directory, or a permissions error logs ERROR and *continues*. | Production installs can run for hours with zero persistence and nobody notices. | Add `SOVRANT_DB_REQUIRE=true` opt-in that causes `InitializeAsync` to throw on any failure. |
| 3 | **No CLI introspection.** There is no `sovrant db status / migrate / backup / vacuum / path` subcommand. | Users have no supported way to see schema version, force a migration, or take a backup before upgrades. | Add a `sovrant db` subcommand group. |
| 4 | **`SOVRANT_DB_PATH` is undocumented in user-facing docs.** It is honored by code but only mentioned in this doc. | Power users can't easily relocate the DB to a network share or alternate disk. | Document in `server.md`, README, and `sovrant --help`. |
| 5 | **No backup-before-migrate.** The migration runner applies V006/V007 in place, with no snapshot of the prior file. | A migration bug or a power failure mid-migration leaves the DB in an undefined state with no rollback. | Add `--backup` flag (default on for major versions) that copies the file before running the runner. |
| 6 | **Migration checksum drift is recorded but not enforced.** `schema_version` stores SHA-256 of each script, but the runner does not raise on mismatch. | A patched migration silently shipping to users would pass checks. | Promote drift detection to a hard error with an opt-out for development. |
| 8 | **Empty `user_id` defaults.** Sessions, audits, and token usage are all written with `user_id = Environment.UserName`. | API-created users (Phase 37) have no derived stats until per-user identity propagates through the agentic loop. | Phase 38 token + identity flow. |
| 9 | **No shared bootstrap helper.** `SqliteStorageProvider.InitializeAsync` is called once, but other code paths (test fixtures, future CLI tools) re-implement parts of the boot flow. | Drift between server boot and test boot can mask bugs. | Extract a `SovrantStorageBootstrap.InitializeAsync` shared helper. |
| 10 | **Connection-per-call with no pool.** Every store opens a fresh `SqliteConnection`. WAL + connection cache helps, but there is no explicit pool. | Under load (server + parallel CLI), we may starve on file handles. | Evaluate `Microsoft.Data.Sqlite` connection pooling; benchmark before/after. |
| 11 | **No `sovrant init` first-boot UX.** A user installing fresh has no clear "the DB is ready" feedback. | Discovery is poor; failures are invisible. | Add `sovrant init` that prints DB path, schema version, and a self-check. |
| 12 | **Health-check coverage is partial.** `/health` does not exercise a write path against the DB. | A DB that is read-only (disk full, permissions) reports healthy. | Add a write-canary to `/health`. |

Roadmap entry: see **Phase 42.5** in `docs/roadmap.md`.

---

## Testing

The persistence layer is exercised by **987 tests** across 9 test projects (all green as of 2026-04-07). Storage-focused suites include:

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
└── evals/results/           ← Eval report JSON files (Phase 42.5: migrate to eval_results table)
```

A fresh boot with no existing DB produces:
- `data/sovrant.db` at schema version 7 (V001–V007 applied in order)
- A `users` row for the OS username (or `SOVRANT_USER_ID`) inserted via `SeedDefaultUser`
- A `workspaces` row `ws-personal-{userId}` inserted via `SeedPersonalWorkspace`
- A `workspace_members` row linking the seeded user as `owner`
