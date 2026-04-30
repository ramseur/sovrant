# Sovrant Config Audit

**Date:** 2026-04-30
**Branch:** `sovrant-openc-dotnet-port`
**Scope:** Every typed config class, settings.json key, and `SOVRANT_*` / `LLM_*` / `ROUTER_*` env var.

## Buckets

- **A. Unified physical config** — must be available before the DB is open (DB path, keystore path, log file, artifacts root, server bearer token). These collapse into one consolidated config file.
- **B. Database (settings table)** — runtime-mutable user preferences and policy. Web/Desktop edit; CLI later. Env vars still work as overrides for 12-factor parity, but the persisted source of truth is the DB.
- **C. Encrypted credential store** — secrets only. Sovrant already has `AesGcmCredentialStore` at `src/Sovrant.Runtime/Mcp/`. Never plaintext in DB or config.
- **D. Env-var only** — CI/operator/bootstrap knobs that should never persist (port, rate limit, runtime mode, deployment topology, NO_COLOR).

## Inventory

| # | Item | Current location(s) | Recommend | Reasoning |
|---|------|-------------------|-----------|-----------|
| **SovrantConfig (root)** | | | | |
| 1 | Model | `SovrantConfig.cs`, env `SOVRANT_MODEL` | B | Runtime-mutable, user preference |
| 2 | MaxTokens | `SovrantConfig.cs` | B | Runtime-mutable per-session tuning |
| 3 | PermissionMode | `SovrantConfig.cs`, default Default | B | Per-session override in `SessionConfig` |
| 4 | RouterMode | ✅ `RouterOptions.Mode` (singleton, mutable); `ROUTER_MODE` / `Router:Mode` resolve at startup | B | Live-mutable; Settings UI hot-swappable |
| 5 | RouterStrategy | ✅ `RouterOptions.Strategy` (singleton, mutable); `ROUTER_STRATEGY` / `Router:Strategy` resolve at startup | B | Live-mutable; Settings UI hot-swappable |
| 6 | BaseUrl | `SovrantConfig.cs` | B | Mutable via Settings UI |
| 7 | ApiKey | `SovrantConfig.cs` | C | Sensitive — encrypted store |
| 8 | DbPath | `SovrantConfig.cs`, `--db-path`, `SOVRANT_DB_PATH` | A | Bootstrap-critical |
| 9 | CompactThreshold | `SovrantConfig.cs`, `SOVRANT_COMPACT_THRESHOLD` | B | Per-workspace tuning |
| 10 | ModelLevels | `SovrantConfig.cs` | B | Tier→Model mappings |
| 11 | TrustBoundary (nested) | `SovrantConfig.cs` | B | Sanitization/ethics/intent toggles |
| 12 | WebSearchOverride | `SovrantConfig.cs` (per-session) | B | Per-session web-search backend |
| 13 | McpServers | `SovrantConfig.cs` (read-only dict) | B | User-managed entries — see Open Q#1 |
| 14 | LspServers | `SovrantConfig.cs` (read-only dict) | B | User-managed language server entries |
| **CredentialConfig** | | | | |
| 15 | LlmApiKey | `CredentialConfig.cs`, `LLM_API_KEY` > `OPENAI_API_KEY` > `PROVIDER_API_KEY` | C | Secret |
| 16 | LlmBaseUrl | `CredentialConfig.cs`, `LLM_BASE_URL` > `OPENAI_BASE_URL` | B | Non-secret endpoint |
| 17 | ProviderApiKey | `CredentialConfig.cs`, `PROVIDER_API_KEY` | C | Secret |
| 18 | ProviderBaseUrl | `CredentialConfig.cs`, `PROVIDER_BASE_URL` | B | Non-secret endpoint |
| 19 | OllamaBaseUrl | `CredentialConfig.cs`, `OLLAMA_BASE_URL` | B | Non-secret endpoint |
| 20 | SovrantToken | `CredentialConfig.cs`, `SOVRANT_TOKEN` (server auth) | A | Bootstrap auth — never persisted |
| 21 | BraveApiKey | `CredentialConfig.cs`, `BRAVE_API_KEY` | C | Secret |
| 22 | FirecrawlApiKey | `CredentialConfig.cs`, `FIRECRAWL_API_KEY` | C | Secret |
| 23 | OpenRouterApiKey | `CredentialConfig.cs`, `OPENROUTER_API_KEY` | C | Secret |
| 24 | WebSearchEnabled (legacy) | `CredentialConfig.cs`, legacy `LLM_WEB_SEARCH=true` | D | Deprecated; migrate to `WebSearchOptions.Backend` |
| **WebSearchOptions** | | | | |
| 25 | Backend | `WebSearchOptions.cs`, mutable `set` | B | Runtime-mutable user preference |
| 26 | ResolvedFromLegacyEnv | `WebSearchOptions.cs` | D | Internal deprecation flag |
| **RoutingConfig** | | | | |
| 27 | IntentRouting | `RoutingConfig.cs`, `.sovrant/routing.json`, `SOVRANT_INTENT_ROUTING` | B | Toggle |
| 28 | DefaultTier | `RoutingConfig.cs` | B | Default tier fallback |
| 29 | AutoTierAssignment | `RoutingConfig.cs` | B | Toggle auto-assignment from pricing |
| 30 | FreeModelsOnly | `RoutingConfig.cs`, `SOVRANT_FREE_MODELS_ONLY` | B | Cost-control preference |
| 31 | TierModels | `RoutingConfig.cs` | B | Explicit tier→model mappings |
| 32 | Escalation | `RoutingConfig.cs` | B | Retry-with-higher-tier toggle |
| 33 | MaxEscalationsPerTurn | `RoutingConfig.cs` | B | Resilience tuning |
| 34 | CustomRules | `RoutingConfig.cs` | B | User-defined regex→tier rules |
| **GovernanceConfig** | | | | |
| 35 | GovernanceLevelName | `GovernanceConfig.cs`, `.sovrant/governance.json`, `SOVRANT_GOVERNANCE_LEVEL` | B | Standard/strict/custom |
| 36 | BlockedCommands | `GovernanceConfig.cs` | B | User security policy |
| 37 | ProtectedFiles | `GovernanceConfig.cs` | B | User security policy |
| 38 | SecretPatterns | `GovernanceConfig.cs` | B | Custom sensitive-data patterns |
| 39 | AuditLog | `GovernanceConfig.cs` | B | Toggle audit logging |
| **SovrantLogConfig** | | | | |
| 40 | MinimumLevel | `SovrantLogConfig.cs`, `SOVRANT_LOG_LEVEL` | D | Operational |
| 41 | FilePath | `SovrantLogConfig.cs`, `SOVRANT_LOG_FILE` | A | Pre-DB-init logging path |
| 42 | ConsoleEnabled | `SovrantLogConfig.cs`, `SOVRANT_LOG_CONSOLE` | D | Operational |
| 43 | Format | `SovrantLogConfig.cs`, `SOVRANT_LOG_FORMAT` | D | Operational (text/json) |
| **TrustBoundaryConfig** | | | | |
| 44 | Enabled | `TrustBoundaryConfig.cs` | B | Master toggle |
| 45 | Sanitizer.Enabled | `TrustBoundaryConfig.cs` | B | Privacy preference |
| 46 | Sanitizer.Mode | `TrustBoundaryConfig.cs` | B | redact/warn/block |
| 47 | Sanitizer.CorporateDomains | `TrustBoundaryConfig.cs` | B | Workspace data classification |
| 48 | Sanitizer.CustomPatterns | `TrustBoundaryConfig.cs` | B | Custom PII/sensitive patterns |
| 49 | Sanitizer.AllowList | `TrustBoundaryConfig.cs` | B | Redaction whitelist |
| 50 | Sanitizer.ExemptProviders | `TrustBoundaryConfig.cs` | B | Skip sanitization (e.g. Ollama) |
| 51 | Sanitizer.LogRedactions | `TrustBoundaryConfig.cs` | B | Audit toggle |
| 52 | EthicalHarness.Enabled | `TrustBoundaryConfig.cs` | B | Safety toggle |
| 53 | EthicalHarness.ClarifyAmbiguous | `TrustBoundaryConfig.cs` | B | Interaction preference |
| 54 | EthicalHarness.BlockHarmfulIntent | `TrustBoundaryConfig.cs` | B | Safety level |
| **HookConfig** | | | | |
| 55 | Hooks | `HookConfig.cs`, `.sovrant/hooks.json` | B | User automation — see Open Q#3 |
| 56 | Event/Command/Matcher | `HookConfig.cs` | B | Hook rule columns |
| **MutableServerConfig (server-only)** | | | | |
| 57 | Model | `MutableServerConfig.cs` (volatile) | B | Server-wide model override |
| 58 | LlmApiKey | `MutableServerConfig.cs` (volatile) | C | API key over HTTP — see Open Q#4 |
| 59 | LlmBaseUrl | `MutableServerConfig.cs` (volatile) | B | Server-wide endpoint |
| 60 | PinnedProvider | `MutableServerConfig.cs` (volatile) | B | Operator routing pin |
| 61 | PermissionMode | `MutableServerConfig.cs` (volatile) | B | Server default |
| **SessionConfig (per-session)** | | | | |
| 62 | Model (override) | `SessionConfig.cs` | B | Per-session shadow |
| 63 | PermissionMode (override) | `SessionConfig.cs` | B | Per-session override |
| 64 | TotalInputTokens / TotalOutputTokens | `SessionConfig.cs` | B | Read-only aggregation |
| **AgentSystemConfig** | | | | |
| 65 | UseIsolatedAgents | `AgentSystemConfig.cs`, `AGENT_MODE` | D | Process isolation backend; startup-fixed |
| 66 | MaxConcurrentAgents | `AgentSystemConfig.cs` | B | Tuning preference |
| 67 | TaskTimeoutSeconds | `AgentSystemConfig.cs` | B | Workspace tuning |
| **SwarmConfig** | | | | |
| 68 | Enabled | `SwarmConfig.cs`, `.sovrant/swarm.json` | B | Feature flag |
| 69 | MaxConcurrent | `SwarmConfig.cs` | B | Wave size tuning |
| 70 | MaxTokenBudget | `SwarmConfig.cs` | B | Cost control |
| 71 | MaxRetries | `SwarmConfig.cs` | B | Resilience tuning |
| 72 | QualityGateEnabled | `SwarmConfig.cs` | B | Feature flag |
| 73 | QualityGateThreshold | `SwarmConfig.cs` | B | 0–10 score threshold |
| 74 | FileLocksEnabled | `SwarmConfig.cs` | B | Safety toggle |
| 75 | DecomposerLevel / WorkerLevel | `SwarmConfig.cs` | B | Tier preferences |
| 76 | TaskTimeoutSeconds | `SwarmConfig.cs` | B | Per-task timeout |
| 77 | Permissions | `SwarmConfig.cs` | B | ask/accept-edits/yolo |
| 78 | TemplateOverrides | `SwarmConfig.cs` | B | Per-task agent template overrides |
| **ExecutorOptions** | | | | |
| 79 | MaxReplans | `ExecutorOptions.cs` | B | Phase 51 tuning |
| 80 | MaxStepRetries | `ExecutorOptions.cs` | B | Tuning |
| **EngineRunContext** | | | | |
| 81 | RuntimeRunId / SessionId / WorkspaceId / ProjectId | `EngineRunContext.cs` | B | Runtime metadata (not user-configurable) |
| **LspServerConfig** | | | | |
| 82 | Language / Command / Args / Env | `LspServerConfig.cs` via `SovrantConfig.LspServers` | B | Per-language server config |
| **McpServerConfig & McpOAuthConfig** | | | | |
| 83 | Command / Args / Env | `McpServerConfig.cs` | B | Server launch config |
| 84 | OAuthConfig (ClientId, ClientSecret, AuthUrl, TokenUrl, Scopes, TokenEnvVar, RedirectUri) | `McpOAuthConfig.cs` | C (secrets) + B (metadata) | Split — see Open Q#1, Q#7 |
| **Server / CLI bootstrap env vars** | | | | |
| 85 | SOVRANT_PORT | Server `Program.cs`, default 5200 | D | Operator override |
| 86 | SOVRANT_MCP_HTTP | Server `Program.cs`, enables `/mcp` SSE | D | Feature toggle |
| 87 | SOVRANT_RATE_LIMIT_RPM | Server, default 60 | D | Operator scaling |
| 88 | SOVRANT_SESSION_TTL_SECONDS | `SessionEvictionService.cs`, default 3600 | B | Workspace tunable; env override OK |
| 89 | SOVRANT_MAX_SESSIONS | `SessionEvictionService.cs`, default 500 | B | Workspace cap; env override OK |
| 90 | SOVRANT_USER_ID | All apps, default OS username | D | Identity boot seed |
| 91 | SOVRANT_WORKSPACE_ID | Desktop/CLI runtime context | D | Context override |
| 92 | SOVRANT_PROJECT_ID | Desktop/CLI runtime context | D | Context override |
| 93 | SOVRANT_ARTIFACTS_BACKEND | `ServiceCollectionExtensions.cs`, default `local` | D | Deployment-time backend |
| 94 | SOVRANT_ARTIFACTS_ROOT | `LocalArtifactStore.cs`, default `~/.sovrant/artifacts` | A | Pre-DB path |
| 95 | SOVRANT_ARTIFACTS_URL_PREFIX | `LocalArtifactStore.cs`, default `/artifacts` | D | Reverse-proxy override |
| 96 | SOVRANT_ARTIFACTS_MIGRATE_LEGACY | `LegacyArtifactImporter.cs` | D | One-time migration toggle |
| 97 | SOVRANT_SESSION_BUDGET_USD | `BudgetEnforcer.cs` | B | Per-workspace cost cap — see Open Q#5 |
| 98 | SOVRANT_PROJECT_BUDGET_USD | `BudgetEnforcer.cs` | B | Per-workspace cost cap — see Open Q#5 |
| 99 | SOVRANT_UNSAFE_DONTASK | `ModeAwarePermissionPolicy.cs` | D | CI override; never persist |
| 100 | SOVRANT_DB_PATH | `ConfigLoader.cs`, `SqliteStorageProvider.cs` | A | Bootstrap-critical |
| 101 | SOVRANT_DB_REQUIRE | `SqliteStorageProvider.cs` | D | Fail-fast deployment flag |
| 102 | SOVRANT_DB_BACKUP_ON_UPGRADE | `SqliteStorageProvider.cs` | D | Operator safety flag |
| 103 | SOVRANT_RUNTIME_MODE | Web `Program.cs`, embedded/remote | D | Deployment-mode toggle |
| 104 | SOVRANT_SERVER_URL | Web `Program.cs` (remote mode) | D | Deployment topology |
| 105 | SOVRANT_API_TOKEN | Web `Program.cs` (remote mode) | D | Client auth, never persist |
| 106 | SOVRANT_INTENT_ROUTING | `RoutingConfigLoader.cs` | D | CI/test toggle |
| 107 | SOVRANT_FREE_MODELS_ONLY | `RoutingConfigLoader.cs` | D | Operator policy override |
| 108 | SOVRANT_GOVERNANCE_LEVEL | `GovernanceConfig.cs` | D | Strict-policy override |
| 109 | SOVRANT_LOG_LEVEL | `SovrantLogConfig.cs` | D | Debug verbosity |
| 110 | SOVRANT_LOG_FILE | `SovrantLogConfig.cs` | A | Pre-DB-init log path |
| 111 | SOVRANT_LOG_CONSOLE | `SovrantLogConfig.cs` | D | Silent/CI toggle |
| 112 | SOVRANT_LOG_FORMAT | `SovrantLogConfig.cs` | D | Structured-log toggle |
| 113 | SOVRANT_WEB_SEARCH | `WebSearchOptions.cs` (auto/brave/firecrawl/native/off/searxng-future) | B | Backend — also DB |
| 114 | NO_COLOR | CLI `Program.cs` | D | Standard no-color.org convention |
| 115 | ROUTER_MODE | ✅ Resolved into `RouterOptions.Mode` at startup | D (env) → B (live in `RouterOptions`) | Env seeds the singleton, then live-mutable |
| 116 | ROUTER_STRATEGY | ✅ Resolved into `RouterOptions.Strategy` at startup | D (env) → B (live in `RouterOptions`) | Env seeds the singleton, then live-mutable |
| 117 | LLM_API_KEY / OPENAI_API_KEY | `CredentialConfig.cs` | C | Secret |
| 118 | LLM_BASE_URL / OPENAI_BASE_URL | `CredentialConfig.cs` | D | Operator/CI override |
| 119 | PROVIDER_API_KEY | `CredentialConfig.cs` | C | Secret |
| 120 | PROVIDER_BASE_URL | `CredentialConfig.cs` | D | Operator override |
| 121 | OLLAMA_BASE_URL | `CredentialConfig.cs` | D | Operator override |
| 122 | LLM_WEB_SEARCH | `CredentialConfig.cs` (deprecated) | D | Legacy; migrate to `SOVRANT_WEB_SEARCH` |

## Open Questions

1. **MCP/LSP server entries** (rows 13, 14, 83, 84): ✅ DONE (V019) — `mcp_servers` and `lsp_servers` tables; `IMcpServerStore` + `ILspServerStore`. `SovrantConfig.McpServers` / `LspServers` removed entirely.
2. **`RouterMode` / `RouterStrategy`** (rows 4, 5, 115, 116): ✅ DONE — extracted into a singleton `RouterOptions` (`Sovrant.Api.Config`) with mutable `Mode`/`Strategy` setters. `SmartRouter` reads from the singleton on every routing decision so the Settings UI can hot-swap values without rebuilding DI. Removed from `SovrantConfig`.
3. **HookConfig** (rows 55, 56): ✅ DONE (V017) — `hooks` table; `IHookStore` + `SqliteHookStore`; `HookRunner` loads at construction.
4. **`MutableServerConfig.LlmApiKey`** (row 58): ✅ DONE — removed from HTTP surface; routed through `ICredentialStore` under key `llm:api_key`.
5. **Budgets** (rows 97, 98): ✅ DONE (V018) — `workspace_settings` table keys `budget.session_usd` / `budget.project_usd`. Env vars override DB.
6. **Session/Project TTL & caps** (rows 88, 89): ✅ DONE (V018) — `workspace_settings` keys `session.ttl_seconds` / `session.max_sessions`. Env vars override DB.
7. **OAuth secrets in MCP config** (row 84): ✅ DONE (V019) — `client_secret` removed from `McpOAuthConfig`; `McpOAuthService` reads it from `ICredentialStore` under `mcp.{name}.client_secret` at token-exchange time.

## Bucket totals

- **A (unified physical config):** 5 — DB path, log file, artifacts root, server bearer token, CLI `--db-path`
- **B (DB settings table):** 82 — model, router prefs, web search, intent routing, custom rules, MCP/LSP entries, trust boundary, swarm, budgets, hooks, governance, tier mappings
- **C (encrypted credential store):** 9 — provider API keys, OAuth secrets, bearer tokens
- **D (env-var only):** 26 — port, rate limit, runtime mode, USER_ID, NO_COLOR, ROUTER_MODE, deployment topology

## Bucket-A Consolidation Plan

Bucket-A items must be available *before* SQLite opens, so they live in a single
physical JSON file rather than the DB. Today they're scattered across
`SovrantConfig.DbPath`, env vars (`SOVRANT_LOG_FILE`, `SOVRANT_ARTIFACTS_ROOT`,
`SOVRANT_TOKEN`, `SOVRANT_DB_PATH`), an embedded `IConfiguration` key
(`Server:Token`), and hardcoded paths (`AesGcmCredentialStore` keystore).

### Target file: `sovrant.config.json`

One typed, layered config holding only the bootstrap-critical paths and the
server bootstrap token. Loaded by a new `BootstrapConfigLoader` before the DI
container builds. Layers (highest precedence first):

1. CLI flags (`--db-path`, `--config`)
2. Environment variables (existing names retained for 12-factor parity)
3. Project file: `./.sovrant/sovrant.config.json` (workspace override)
4. User file: `~/.sovrant/sovrant.config.json` (user default)
5. Built-in defaults

### Fields

| Field | Default | Env override | CLI override |
|-------|---------|--------------|--------------|
| `dbPath` | `~/.sovrant/data/sovrant.db` | `SOVRANT_DB_PATH` | `--db-path` |
| `logFile` | `~/.sovrant/logs/sovrant-{Date}.log` | `SOVRANT_LOG_FILE` | — |
| `artifactsRoot` | `~/.sovrant/artifacts` | `SOVRANT_ARTIFACTS_ROOT` | — |
| `keystorePath` | `~/.sovrant/credentials/.keystore` | `SOVRANT_KEYSTORE_PATH` (new) | — |
| `serverToken` | `""` (no auth) | `SOVRANT_TOKEN` | — |

The keystore path is currently hardcoded inside `AesGcmCredentialStore`; this
plan promotes it to a real configurable so site admins can move keystores
between machines or onto a mounted secret volume.

### What we delete / replace

- `SovrantConfig.DbPath` (init-only) — bootstrap concern, doesn't belong on the
  runtime config object. Move to `BootstrapConfig.DbPath`.
- `CredentialConfig.SovrantToken` — already env-only; the resolver moves into
  `BootstrapConfigLoader` so the field disappears from `CredentialConfig`.
- `SovrantLogConfig.FromEnvironment()` — keeps the env-only path internally, but
  is fed by `BootstrapConfig.LogFile` when no env override is set.
- `LocalArtifactStore` constructor `artifactsRoot` parameter — wired from
  `BootstrapConfig.ArtifactsRoot` instead of reading the env var directly.
- `AesGcmCredentialStore` keystore path — accepts a path parameter; default
  behaviour preserved when `keystorePath` is unset.

### Example file (ships at `docs/sovrant.config.example.json`)

```json
{
  "dbPath": "~/.sovrant/data/sovrant.db",
  "logFile": "~/.sovrant/logs/sovrant-{Date}.log",
  "artifactsRoot": "~/.sovrant/artifacts",
  "keystorePath": "~/.sovrant/credentials/.keystore",
  "serverToken": ""
}
```

`~` expands to the user's home directory; `{Date}` in `logFile` is replaced at
write time by the logging framework. All five fields are optional — omit any
to fall back to the default.
