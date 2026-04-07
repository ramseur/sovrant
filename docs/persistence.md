# Sovrant — Persistence Layer

**Phases 32, 35, 36, 37 (active) — Phase 42.5 planned** | **Last updated:** 2026-04-06

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

All V006/V007 statements are **additive** (`CREATE TABLE`, `CREATE INDEX IF NOT EXISTS`), so a database created at V005 or earlier upgrades cleanly on next boot — no manual intervention.

Migrations are idempotent — running `InitializeAsync` multiple times is safe. The runner skips already-applied versions and records SHA-256 checksums of each script in `schema_version` to detect drift (drift detection itself is a Phase 42.5 item — today the runner records the checksum but does not raise on mismatch).

### Future-proofing

The schema is designed upfront so that Phases 33–37 can ship without `ALTER TABLE`:

- **`workspace_id`** and **`project_id`** existed as nullable columns on sessions, token_usage, audit, credentials, memory, swarm, and eval tables in V001. V006/V007 only added indexes, never columns.
- **`users.role`**, **`users.team`**, **`users.status`** existed in V001. **Phase 37 added the API surface only — no schema changes.**
- **RBAC tables** (`roles`, `permissions`, `role_permissions`, `user_roles`) exist in V001 (still empty). Phase 40 will populate them.
- **`api_tokens`** table exists in V001 (still empty). Phase 38 will populate it.

---

## SQLite Configuration

Every connection applies these PRAGMAs:

```sql
PRAGMA journal_mode = WAL;        -- Write-Ahead Logging for concurrent reads
PRAGMA synchronous = NORMAL;      -- Balance durability/performance
PRAGMA foreign_keys = ON;         -- Enforce referential integrity
PRAGMA busy_timeout = 5000;       -- Wait up to 5s on lock contention
PRAGMA cache_size = -20000;       -- 20 MB page cache
```

**WAL mode** is critical for CLI + server sharing the same database file — multiple readers never block each other, and a single writer doesn't block readers.

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
| Swarm sessions | `~/.sovrant/swarm/sessions/` | JSONL event replay (SQLite table ready for future migration) |
| Eval definitions | `.sovrant/evals/*.json` | Human-authored, version-controlled |
| Eval results | `~/.sovrant/evals/results/` | File-based (SQLite table ready for future migration) |

---

## Known Concerns & Future Work (Phase 42.5)

The following concerns were surfaced during the Phase 37 audit of the SQLite layer. None are blocking, but all are tracked under **Phase 42.5 — Database Lifecycle: Setup, Upgrade Safety & Introspection** in the roadmap.

| # | Concern | Why it matters | Planned mitigation |
|---|---|---|---|
| 1 | **Parallel JSONL persistence is still wired in.** `SOVRANT_SESSION_JSONL` and `SOVRANT_AUDIT_JSONL` dual-write to flat files. | Two stores of truth drift; consumers don't know which is canonical. | Consolidate into SQLite as the sole source; keep dual-write only as a one-shot migration tool. |
| 2 | **Silent init failures.** A bad `SOVRANT_DB_PATH`, an unwritable home directory, or a permissions error logs ERROR and *continues*. | Production installs can run for hours with zero persistence and nobody notices. | Add `SOVRANT_DB_REQUIRE=true` opt-in that causes `InitializeAsync` to throw on any failure. |
| 3 | **No CLI introspection.** There is no `sovrant db status / migrate / backup / vacuum / path` subcommand. | Users have no supported way to see schema version, force a migration, or take a backup before upgrades. | Add a `sovrant db` subcommand group. |
| 4 | **`SOVRANT_DB_PATH` is undocumented in user-facing docs.** It is honored by code but only mentioned in this doc. | Power users can't easily relocate the DB to a network share or alternate disk. | Document in `server.md`, README, and `sovrant --help`. |
| 5 | **No backup-before-migrate.** The migration runner applies V006/V007 in place, with no snapshot of the prior file. | A migration bug or a power failure mid-migration leaves the DB in an undefined state with no rollback. | Add `--backup` flag (default on for major versions) that copies the file before running the runner. |
| 6 | **Migration checksum drift is recorded but not enforced.** `schema_version` stores SHA-256 of each script, but the runner does not raise on mismatch. | A patched migration silently shipping to users would pass checks. | Promote drift detection to a hard error with an opt-out for development. |
| 7 | **Swarm sessions still bypass the DB.** `~/.sovrant/swarm/sessions/*.jsonl` is the only home for swarm events even though `swarm_events` exists in V005. | Swarm history is invisible to per-user/per-workspace queries and excluded from backups. | Swap `JsonlSwarmEventStore` for a SQLite-backed implementation. |
| 8 | **Empty `user_id` defaults.** Sessions, audits, and token usage are all written with `user_id = Environment.UserName`. | API-created users (Phase 37) have no derived stats until per-user identity propagates through the agentic loop. | Phase 38 token + identity flow. |
| 9 | **No shared bootstrap helper.** `SqliteStorageProvider.InitializeAsync` is called once, but other code paths (test fixtures, future CLI tools) re-implement parts of the boot flow. | Drift between server boot and test boot can mask bugs. | Extract a `SovrantStorageBootstrap.InitializeAsync` shared helper. |
| 10 | **Connection-per-call with no pool.** Every store opens a fresh `SqliteConnection`. WAL + connection cache helps, but there is no explicit pool. | Under load (server + parallel CLI), we may starve on file handles. | Evaluate `Microsoft.Data.Sqlite` connection pooling; benchmark before/after. |
| 11 | **No `sovrant init` first-boot UX.** A user installing fresh has no clear "the DB is ready" feedback. | Discovery is poor; failures are invisible. | Add `sovrant init` that prints DB path, schema version, and a self-check. |
| 12 | **Health-check coverage is partial.** `/health` does not exercise a write path against the DB. | A DB that is read-only (disk full, permissions) reports healthy. | Add a write-canary to `/health`. |

Roadmap entry: see **Phase 42.5** in `docs/roadmap.md`.

---

## Testing

The persistence layer is exercised by **985 tests** across 9 test projects (all green as of 2026-04-06). Storage-focused suites include:

| Test Class | Validates |
|---|---|
| `SqliteStorageProviderTests` | DB creation, schema version, idempotent init, transactions, graceful error handling |
| `SqliteSessionStoreTests` | Append/load round-trip, ordering, optional fields, null handling, list sessions |
| `SqliteMemoryStoreTests` | Summaries, patterns, instincts, reinforcement, correction, pruning |
| `SqliteAuditStoreTests` | Governance events, bash commands, batch writes |
| `SqliteTokenUsageStoreTests` | Record/aggregate, empty session, cost tracking |
| `MigrationRunnerTests` | All migrations apply in order, idempotency, expected tables present |
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
├── swarm/sessions/          ← Swarm event JSONL replay (Phase 42.5: migrate to swarm_events table)
└── evals/results/           ← Eval report JSON files (Phase 42.5: migrate to eval_results table)
```

A fresh boot with no existing DB produces:
- `data/sovrant.db` at schema version 7 (V001–V007 applied in order)
- A `users` row for the OS username (or `SOVRANT_USER_ID`) inserted via `SeedDefaultUser`
- A `workspaces` row `ws-personal-{userId}` inserted via `SeedPersonalWorkspace`
- A `workspace_members` row linking the seeded user as `owner`
