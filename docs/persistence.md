# Sovrant — Persistence Layer

**Phase 32** | **Last updated:** 2026-04-06

This document describes how Sovrant stores durable operational data. All persistent state (sessions, memory, audit, credentials, token usage) is managed by a SQLite database. Flat-file stores (JSONL, JSON) remain available as a dual-write option during migration.

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
        ┌───────────┬───────────┬───┴────┬──────────┬────────────┐
        ▼           ▼           ▼        ▼          ▼            ▼
  ISessionStore IMemoryStore IAuditStore ITokenUsage ICredential  (future)
  SqliteSession SqliteMemory SqliteAudit SqliteToken SqliteCredl  SwarmEvent
  Store         Store        Store       UsageStore  Store        EvalResult
```

Every domain store receives an `ISqliteConnectionFactory` via constructor injection, creates a new connection per operation, and uses parameterized queries exclusively (no string interpolation in SQL).

---

## Database Location

| Scenario | Path |
|---|---|
| Default (CLI + server) | `~/.sovrant/data/sovrant.db` |
| Custom | Set `SOVRANT_DB_PATH` environment variable |
| Tests | In-memory (`file:sovrant_test_{guid}?mode=memory&cache=shared`) |

The data directory is created automatically on first run. If the directory cannot be created or the database cannot be opened, Sovrant logs a friendly error and continues — no crash on a bad path.

---

## Schema Migrations

Migrations are embedded SQL resources named `V{NNN}__{description}.sql` inside the `Sovrant.Runtime` assembly. The `MigrationRunner` applies them in version order and tracks each in a `schema_version` table with SHA-256 checksums.

| Version | File | Tables Created |
|---|---|---|
| V001 | `V001__foundation.sql` | `schema_version`, `users`, `workspaces`, `workspace_members`, `workspace_config`, `workspace_invites`, `projects`, `project_members`, `project_config`, `config`, `api_tokens`, `roles`, `permissions`, `role_permissions`, `user_roles`, `audit_governance`, `audit_bash` |
| V002 | `V002__sessions.sql` | `sessions`, `session_entries`, `session_entries_fts` (FTS5), `token_usage` |
| V003 | `V003__memory.sql` | `session_summaries`, `learned_patterns`, `instincts` |
| V004 | `V004__credentials.sql` | `credentials` |
| V005 | `V005__swarm_evals.sql` | `swarm_events`, `eval_runs`, `eval_results` |

Migrations are idempotent — running `InitializeAsync` multiple times is safe. The runner skips already-applied versions.

### Future-proofing

The schema is designed upfront to accommodate Phases 33-37 without `ALTER TABLE`:

- **`workspace_id`** and **`project_id`** exist as nullable columns on sessions, token_usage, audit, credentials, memory, swarm, and eval tables NOW. Phase 33/34 backfill them.
- **`users.role`**, **`users.team`**, **`users.status`** columns exist NOW with defaults. Phase 35 uses them.
- **RBAC tables** (`roles`, `permissions`, `role_permissions`, `user_roles`) exist NOW (empty). Phase 37 seeds them.
- **`api_tokens`** table exists NOW (empty). Phase 35 populates it.

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
EvalResultStore        →  IEvalResultStore   (file-based, interface extracted)
```

Storage is initialized during `InitializeRuntimeAsync` — migrations run before MCP servers connect or any request is served.

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

## Testing

31 dedicated storage tests cover:

| Test Class | Tests | Validates |
|---|---|---|
| `SqliteStorageProviderTests` | 7 | DB creation, schema version, idempotent init, transactions, graceful error handling |
| `SqliteSessionStoreTests` | 7 | Append/load round-trip, ordering, optional fields, null handling, list sessions |
| `SqliteMemoryStoreTests` | 8 | Summaries, patterns, instincts, reinforcement, correction, pruning |
| `SqliteAuditStoreTests` | 3 | Governance events, bash commands, batch writes |
| `SqliteTokenUsageStoreTests` | 3 | Record/aggregate, empty session, cost tracking |
| `MigrationRunnerTests` | 3 | All 5 migrations, idempotency, expected tables |

All server integration tests use isolated in-memory SQLite databases (unique per test factory instance via `Cache=Shared` named memory DBs).

---

## Disk Layout

After a fresh install and first run, `~/.sovrant/` contains:

```
~/.sovrant/
├── data/
│   └── sovrant.db          ← SQLite database (all operational data)
├── credentials/
│   └── .keystore            ← AES-256-GCM master key (hex)
├── logs/
│   └── sovrant-2026-04-06.log
├── memory.md                ← Global memory (human-edited)
├── sessions/                ← (legacy, only if SOVRANT_SESSION_JSONL=true)
├── audit/                   ← (legacy, only if SOVRANT_AUDIT_JSONL=true)
├── swarm/sessions/          ← Swarm event JSONL replay
└── evals/results/           ← Eval report JSON files
```
