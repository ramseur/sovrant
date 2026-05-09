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

## Resolved Items (historical — all complete by Phase 90)

The following items were tracked as open during Phase 87/88 and have all shipped:

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

## Bucket-A Consolidation Plan ✅ DONE

Bucket-A items must be available *before* SQLite opens, so they live in a single
physical JSON file rather than the DB. They're now consolidated into
`BootstrapConfig` / `BootstrapConfigLoader` backed purely by environment
variables and an optional `.env` file. Previously scattered across
`SovrantConfig.DbPath`, env vars (`SOVRANT_LOG_FILE`, `SOVRANT_ARTIFACTS_ROOT`,
`SOVRANT_TOKEN`, `SOVRANT_DB_PATH`), an embedded `IConfiguration` key
(`Server:Token`), and hardcoded paths (credential store keystore).

### Configuration source: environment variables + `.env`

Bootstrap config reads from environment variables only. A `.env` file in the
current working directory is loaded first; variables already set in the process
environment take precedence (so CI secrets always win). Layers (highest precedence first):

1. CLI flag: `--db-path <path>`
2. Process environment variables
3. `.env` file in the current working directory
4. Built-in defaults

### Fields

| Variable | Default | CLI override |
|----------|---------|--------------|
| `SOVRANT_DB_PATH` | `~/.sovrant/data/sovrant.db` | `--db-path` |
| `SOVRANT_LOG_FILE` | `~/.sovrant/logs/sovrant-{Date}.log` | — |
| `SOVRANT_ARTIFACTS_ROOT` | `~/.sovrant/artifacts` | — |
| `SOVRANT_KEYSTORE_PATH` | `~/.sovrant/credentials/.keystore` | — |
| `SOVRANT_TOKEN` | `""` (no auth) | — |
| `SOVRANT_TLS_CERT` | `null` (TLS disabled) | — |
| `SOVRANT_TLS_CERT_PASSWORD` | `null` | — |
| `SOVRANT_TLS_KEY` | `null` | — |
| `SOVRANT_TLS_HTTPS_PORT` | `5443` (Server) / `5101` (Web) | — |

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
  behaviour preserved when `SOVRANT_KEYSTORE_PATH` is unset.
- `sovrant.config` JSON file and `BootstrapConfigLoader` file-loading code —
  replaced by `.env` file support.

### Example (`.env.example` ships in the repo root)

```dotenv
LLM_API_KEY=
SOVRANT_TOKEN=

# Storage paths — uncomment to override defaults
# SOVRANT_DB_PATH=~/.sovrant/data/sovrant.db
# SOVRANT_LOG_FILE=~/.sovrant/logs/sovrant-{Date}.log
# SOVRANT_ARTIFACTS_ROOT=~/.sovrant/artifacts
# SOVRANT_KEYSTORE_PATH=~/.sovrant/credentials/.keystore

# TLS — disabled by default; set SOVRANT_TLS_CERT to enable HTTPS
# SOVRANT_TLS_CERT=/etc/sovrant/certs/server.pfx
# SOVRANT_TLS_HTTPS_PORT=5443
```

`{Date}` in `SOVRANT_LOG_FILE` is replaced at write time by the logging
framework. TLS is disabled by default; for development use
`dotnet dev-certs https --trust` (no env vars needed).

## Bucket-C Credential Store ✅ DONE

All five Bucket-C secrets now flow through the encrypted `ICredentialStore` with
runtime override via env var. `sovrant auth set <name>` writes the value through
`AesGcmCredentialStore`; consumers read it back via the env > store > snapshot
chain so a `auth set llm <new-key>` rotation takes effect on the next request
without restarting the process.

| Row | Key | Store key (`CredentialKeys`) | CLI name | Env override(s) | Consumer |
|-----|-----|------------------------------|----------|-----------------|----------|
| 15  | `LlmApiKey`        | `llm.api_key`               | `llm`        | `LLM_API_KEY` > `OPENAI_API_KEY` > `PROVIDER_API_KEY` | `CredentialStoreAuthProvider` (primary `IAuthProvider`) |
| 17  | `ProviderApiKey`   | `provider.api_key`          | `provider`   | `PROVIDER_API_KEY`     | `ProviderApiProvider.BuildRequestAsync` (per-request `x-api-key`) |
| 21  | `BraveApiKey`      | `websearch.brave_api_key`   | `brave`      | `BRAVE_API_KEY`        | `WebSearchTool` + `WebSearchCommand` (status display) |
| 22  | `FirecrawlApiKey`  | `websearch.firecrawl_api_key` | `firecrawl` | `FIRECRAWL_API_KEY`    | `WebSearchTool` + `WebSearchCommand` (status display) |
| 23  | `OpenRouterApiKey` | `openrouter.api_key`        | `openrouter` | `OPENROUTER_API_KEY`  | `LiveModelMetadataFetcher.FetchAsync` |

**Wiring summary:**
- `Sovrant.Api.Auth.IApiKeyResolver` (interface) lives in `Sovrant.Api` so consumers
  there don't need a Runtime reference. `Sovrant.Api` registers the bootstrap
  `EnvApiKeyResolver` (env > snapshot only); `Sovrant.Runtime` overrides it with
  `CredentialStoreApiKeyResolver` once the encrypted store is open.
- `CredentialResolver.ResolveAsync` (in `Sovrant.Runtime.Mcp`) is the single helper
  for one-shot non-cached lookups — it powers both the `IApiKeyResolver` adapter
  and direct callers in `Sovrant.Tools` / `Sovrant.Commands`.
- The primary LLM key still uses the cached `CredentialStoreAuthProvider` because
  it's hit on every model call; secondary keys use `CredentialResolver` directly
  (no caching, since they're rare/manual code paths and hot-rotation matters more
  than a per-call store hit).
- `ProviderApiProvider`'s `x-api-key` header is now built per-request inside
  `BuildRequestAsync` instead of baked into `HttpClient.DefaultRequestHeaders.Authorization`
  (which used a malformed scheme and missed env-var rotations).

## Bucket-B Step 1 — simple scalars ✅ DONE

Five tunable scalars now resolve through `WorkspaceSettingsResolver`
(`Sovrant.Runtime.Workspaces`) with the chain **env var > `IWorkspaceSettingsStore`
global row > hardcoded fallback**. The store is read once at DI construction
time and cached for the process lifetime — matches the pre-existing
`BudgetEnforcer` pattern; hot-swap from a Settings UI is deferred to step 3
where it's actually needed.

| Row | Setting | Store key (`WorkspaceSettingsKeys`) | Env override | Default | Consumer |
|-----|---------|--------------------------------------|--------------|---------|----------|
| 30  | Re-plan budget               | `executor.max_replans`        | `SOVRANT_EXECUTOR_MAX_REPLANS`     | `3`    | `ExecutorOptions.Resolve` → DI factory in `Sovrant.Runtime` |
| 31  | Step retry budget            | `executor.max_step_retries`   | `SOVRANT_EXECUTOR_MAX_STEP_RETRIES`| `2`    | `ExecutorOptions.Resolve` |
| 32  | Max concurrent agents        | `agent.max_concurrent`        | `SOVRANT_AGENT_MAX_CONCURRENT`     | `5`    | `AgentSystemConfig.Resolve` → deferred DI factory in `AddOrchestrationSystem` |
| 33  | Per-task agent timeout (sec) | `agent.task_timeout_seconds`  | `SOVRANT_AGENT_TASK_TIMEOUT_SECONDS`| `120` | `AgentSystemConfig.Resolve` |
| 34  | Compaction threshold (input tokens) | `compact.threshold`    | `SOVRANT_COMPACT_THRESHOLD`        | `SovrantConfig.CompactThreshold` (snapshot) | `ConversationRuntime` ctor |

**Wiring notes:**
- `WorkspaceSettingsResolver.ResolveInt` / `ResolveDecimalOrNull` / `ResolveString`
  (synchronous-over-async) is the single entry point so the precedence chain
  stays consistent across consumers. Catches `InvalidOperationException`,
  `DbException`, `IOException`, `UnauthorizedAccessException` from the store
  so fresh-install / unmigrated-DB cases fall through to the fallback.
- `AgentSystemConfig.Resolve(IWorkspaceSettingsStore?)` replaces the eager
  `FromEnvironment()` call in `AddOrchestrationSystem`; the DI registration
  is deferred so the settings store is available at first resolution.
- `ConversationRuntime` accepts an optional `IWorkspaceSettingsStore? settings`
  ctor parameter (last position). `SovrantConfig.CompactThreshold` is preserved
  as the snapshot fallback so existing `settings.json` files keep working.
- Tests: `WorkspaceSettingsResolverTests` (14 cases) covers env-wins, store-wins,
  fallback, null-store, bad-env, bad-store-value, throwing-store, and culture
  invariance. Lives in `tests/Sovrant.Runtime.Tests/Workspaces`.

**Out of scope for step 1 (deferred):**
- Governance scalars (rows 35-39 — `BlockedCommands`, `ProtectedFiles`,
  `SecretPatterns`, `AuditLog`, `GovernanceLevelName`) — these are list/string
  shapes that need migration helpers, not just scalar resolves. Step 2.
- TrustBoundary settings (rows 44-54) and the Settings UI write path — step 3.
- Hot-reload from the Settings UI — every Bucket-B consumer reads at construction
  and caches; settings changes still require a restart until step 3 introduces
  mutable singletons where it matters.

## Bucket-B Step 2 — Governance ✅ DONE

Rows 35-39 (`GovernanceConfig`) now resolve through the same env > DB > fallback
chain as step 1, with the JSON-file pair (`~/.sovrant/governance.json` global +
`<workspace>/.sovrant/governance.json` project-local) demoted to **bootstrap
fallback**. Existing JSON setups keep working; the first save through the
Settings UI promotes the values to the DB and they take precedence on next load.

| Row | Setting | Store key (`WorkspaceSettingsKeys`) | Env override | Default | Shape |
|-----|---------|--------------------------------------|--------------|---------|-------|
| 35  | Governance level    | `governance.level`              | `SOVRANT_GOVERNANCE_LEVEL`           | `standard`     | string |
| 36  | Blocked commands    | `governance.blocked_commands`   | `SOVRANT_GOVERNANCE_BLOCKED_COMMANDS`| `[]`           | JSON array (DB) / JSON or comma-separated (env) |
| 37  | Protected files     | `governance.protected_files`    | `SOVRANT_GOVERNANCE_PROTECTED_FILES` | `[]`           | JSON array / JSON or comma-separated |
| 38  | Secret patterns     | `governance.secret_patterns`    | `SOVRANT_GOVERNANCE_SECRET_PATTERNS` | `[]`           | JSON array / JSON or comma-separated |
| 39  | Audit log           | `governance.audit_log`          | `SOVRANT_GOVERNANCE_AUDIT_LOG`       | `true`         | bool (`true`/`false`/`1`/`0`/`yes`/`no`/`on`/`off`, case-insensitive) |

**New resolver helpers:**
- `WorkspaceSettingsResolver.ResolveBool` — same precedence chain; common
  truthy/falsy forms accepted, unparseable values fall through to next layer.
- `WorkspaceSettingsResolver.ResolveStringList` — DB stores JSON arrays;
  env accepts JSON or comma-separated for shell-friendliness; lists fully
  replace (no merge) when a higher-priority source is set.

**Wiring notes:**
- `GovernanceConfig.Load` now has a two-arg overload taking `IWorkspaceSettingsStore?`;
  the legacy single-arg `Load(workingDirectory)` calls it with `settings: null`
  so existing call sites stay green.
- DI in `Sovrant.Runtime.ServiceCollectionExtensions` resolves the store via
  `sp.GetService<IWorkspaceSettingsStore>()` so the deferred-factory pattern
  works regardless of registration order.
- `GovernanceConfig.SaveToStoreAsync(store)` is the single write entry point
  used by both Web and Desktop Settings UIs.
- **Settings UI write paths are now DB-backed:**
  - `Sovrant.Web/Components/Pages/Governance.razor` — injects
    `IWorkspaceSettingsStore`, calls `SaveToStoreAsync` on save instead of
    serializing to `~/.sovrant/governance.json`.
  - `Sovrant.Desktop/ViewModels/GovernanceViewModel.cs` — takes
    `IWorkspaceSettingsStore?` via DI, same write path.
- **Reading list values:** the loader replaces (not merges) the JSON-file list
  when DB / env values are set. This matches the "DB is the persistent source
  of truth" model and avoids silent contamination by leftover JSON-file entries.

**Tests:** 14 new resolver tests (bool truthy/falsy forms, env-wins, store-wins,
fallback, bad values; list JSON/CSV parsing, fallback identity, bad JSON,
empty-string-as-unset) + 5 new `GovernanceConfigTests` cases (DB overrides
JSON, DB-missing falls back to JSON, null-store legacy parity, full save round-trip,
all-fields persistence). All 859 Runtime, 229 Agents, and 164 Server tests pass.

**Out of scope for step 2 (deferred — see Roadmap below):**
- TrustBoundary settings (rows 44-54) and Web UI — step 3 ✅ done.
- Hot-reload across all Bucket-B settings — step 4 (cross-cutting).
- Desktop TrustBoundary UI — step 5.
- Settings UI for step-1 scalars (executor/agent/compaction) — step 6 (optional).
- Per-workspace overrides (vs the current global-only model) — step 7.

## Bucket-B Roadmap

Steps 1 and 2 established the env > DB > snapshot pipeline and the resolver helpers.
The remaining work splits along three axes — **scope of settings**, **read vs write**,
and **single-process vs cross-process consistency**. This roadmap orders the
remaining steps so each one ships an independently usable improvement and so that
foundational primitives (mutable runtime singletons, the per-workspace dimension)
land before the UI work that depends on them.

### Step 3 — TrustBoundary read path + Web UI ✅ DONE

Rows 44-54 (`TrustBoundaryConfig`, `SanitizerConfig`, `IntentVerificationConfig`)
now flow through the same env > DB > snapshot > defaults pipeline as Bucket-B
steps 1 and 2.

**Keys added to `WorkspaceSettingsKeys`:**
- `trustboundary.enabled`
- `trustboundary.sanitizer.{enabled,mode,corporate_domains,custom_patterns,allow_list,exempt_providers,log_redactions}`
- `trustboundary.intent.{enabled,clarify_ambiguous,block_harmful}`

**Read path:** `TrustBoundaryConfig.Resolve(snapshot, settings)` constructs a
fresh `TrustBoundaryConfig` (and child `SanitizerConfig` / `IntentVerificationConfig`
instances, since all properties are `init`-only) layered as env > DB > snapshot.
The DI registration in `ServiceCollectionExtensions` now resolves a singleton
`TrustBoundaryConfig` from `Resolve(config.TrustBoundary, IWorkspaceSettingsStore?)`
and downstream sanitizer / ethical-harness / intent registrations consume that
singleton — `config.TrustBoundary` is no longer read directly. The
`EthicalHarness` branch is currently snapshot-only (no DB keys are defined yet).

**`CustomPatterns` (row 48):** JSON-encoded `List<CustomPattern>` (record with
`Name`, `RegexPattern`, `Action`). Env vars accept the same JSON; comma-separated
form is *not* supported for this list because each entry has structure.

**Write path:** `TrustBoundaryConfig.SaveToStoreAsync(store)` writes the 11 keys
to the global workspace row. Validation runs first (Q3 decision — validate on
save) and rejects unknown sanitizer modes and non-compiling custom regexes
before any DB write happens, so a partial half-saved state is impossible.

**Validation also retro-fitted to `GovernanceConfig.SaveToStoreAsync`:** rejects
unknown `GovernanceLevelName` values and non-compiling secret regexes.

**Web UI:** new `/trust-boundary` page (`Sovrant.Web/Components/Pages/TrustBoundaryPage.razor`)
with master-toggle, sanitizer mode/toggles, list editors for corporate domains,
allow list, exempt providers, custom patterns, and the three intent toggles.
Linked from the governance secondary panel (`GovernancePanel.razor`). Direct
DI inject of `IWorkspaceSettingsStore` and `SovrantConfig` — no separate HTTP
endpoint needed for Blazor Server.

**Tests:** 8 new `TrustBoundaryConfigTests` (null-store, store-overrides-snapshot,
custom-pattern JSON round-trip, save persists all fields, save→load round-trip,
invalid mode rejected, invalid regex rejected, valid config passes Validate) +
2 new `GovernanceConfigTests` (invalid level rejected, invalid secret regex
rejected). Full Runtime suite: 869 passed, 0 failed.

**Acceptance:** TrustBoundary fields persist to the DB and load on next process
start. Restart still required for the change to take effect at runtime — that's
step 4.

### Step 4 — Hot-reload via mutable runtime singletons (cross-cutting) ✅ DONE

**Semantics (decided):** *honour on next turn*. In-flight requests keep their
captured config; the next call into a live consumer sees the new values. We do
not drop or re-initialise live sessions.

**What shipped:**

1. `Sovrant.Runtime.Workspaces.ILiveSettings<T>` (`Current` + `OnChanged(handler)`
   returning `IDisposable`) and `LiveSettings<T>` (the mutable wrapper) plus
   `LiveSettingsRegistry` (fan-out `ReloadAll`). `LiveSettings.Static<T>` covers
   tests / call sites that hold a static snapshot.
2. Cheap-tier wrappers (read `Current` on each use): `ExecutorOptions`,
   `CompactionSettings`, `AgentSystemConfig` — `LlmExecutor`,
   `ConversationRuntime`, `OrchestrationCoordinator`,
   `ProcessBasedOrchestrationSystem` migrated. `MaxConcurrentAgents` stays
   captured at construction (semaphore can't be resized mid-flight) — documented
   in-source.
3. Expensive-tier wrappers (subscribe and atomically swap internal state via
   `Volatile.Read/Write` over a `Snapshot` record):
   - `GovernanceMonitor` swaps `RuleSet(Config, Level, SecretDetector,
     CommandDetector, ConfigProtection)`.
   - `PromptSanitizer` swaps the detector array.
   - `ContentPolicyEngine` swaps `Snapshot(Policy, CustomBlockedRegexes)`.
   Each implements `IDisposable` to drop its `OnChanged` subscription.
4. DI: `LiveSettingsRegistry` registered as a singleton. Each setting registers a
   `LiveSettings<T>` (self-registering with the registry) plus an
   `ILiveSettings<T>` alias and a convenience `T` resolver from `Current`.
5. Settings UI fan-out: `Sovrant.Web` `Governance.razor` and
   `TrustBoundaryPage.razor` plus the Desktop `GovernanceViewModel` call
   `LiveSettingsRegistry.ReloadAll()` immediately after `SaveToStoreAsync`.

**Tests:** Added `LiveSettingsTests` (5 tests over the primitive),
`HotReloadEndToEndTests` (governance + sanitizer end-to-end through
`SaveToStoreAsync` → `ReloadAll`), plus per-component hot-reload tests in
`GovernanceMonitorTests`, `PromptSanitizerTests`, `ContentPolicyEngineTests`.
Full Runtime suite: 877 passed, 0 failed.

**Acceptance:** Save in the Settings UI → next call into the affected service
sees the new values, no restart. Validated end-to-end.

### Step 5 — Desktop TrustBoundary UI ✅ DONE

Avalonia view + view-model now mirror the Web `/trust-boundary` page.

**Shipped:**

1. `Sovrant.Desktop/ViewModels/TrustBoundaryViewModel.cs` — observable
   properties for the master toggle, sanitizer enable/mode/log-redactions,
   the three intent toggles, plus `ObservableCollection`s for corporate
   domains, allow-list, exempt providers, and `CustomPattern` rows. Reads via
   `TrustBoundaryConfig.Resolve(_config.TrustBoundary, _settings)`. Save runs
   `TrustBoundaryConfig.SaveToStoreAsync(store)` then
   `LiveSettingsRegistry.ReloadAll()` so step 4's hot-reload kicks in
   immediately. Validation errors surface via `StatusMessage`.
2. `Sovrant.Desktop/Views/TrustBoundaryView.axaml` (+ `.axaml.cs`) — scrollable
   form with Add/Remove rows for the three string lists and a 4-column grid
   editor for `CustomPattern` (name, regex, action). Themed via the existing
   `Surface*` / `Brand*` / `Status*` `DynamicResource` tokens.
3. DI: `services.AddTransient<TrustBoundaryViewModel>()` in `App.axaml.cs`.
4. Nav: `MainViewModel.OnNavigationRequested` resolves `"TrustBoundary"`
   to the new VM. `Views/MainWindow.axaml` declares the
   `vm:TrustBoundaryViewModel` → `views:TrustBoundaryView` `DataTemplate`.
   `Views/GovernancePanelView.axaml` adds a "🔒  Trust Boundary" button to the
   governance group panel between Governance and Diagnostics.

**Acceptance:** Navigate Governance group → Trust Boundary → edit values →
Save → next conversation turn picks up the new sanitizer / intent settings
without restart (via step 4's `ReloadAll`).

### Step 6 — Settings UI for step-1 scalars (optional)

Currently the executor / agent / compaction tunables (rows 30-32, 33, 34) have
no UI at all — env vars and direct `workspace_settings` rows are the only
write paths. If users want to tune these without leaving the app, add a single
"Performance & Limits" panel to Web + Desktop. This is genuinely optional;
power users will edit env vars and casual users won't touch these.

### Step 7 — Per-workspace overrides

Every step-1/2/3 consumer reads `GetGlobalAsync` (the empty-string workspace
row). The `IWorkspaceSettingsStore` interface already supports per-workspace
rows; what's missing is:

1. A way for consumers to know which workspace they're operating in
   (`WorkspaceContext` exists but isn't threaded through every config consumer).
2. UI affordance — "this workspace" vs "default for all workspaces" toggle on
   each settings page.
3. A merge resolver that reads the workspace row, falls back to the global row,
   and finally to the existing snapshot/defaults chain.

This is a real UX win for shared installations but is a deeper change than the
prior steps. Recommend deferring until at least one user asks for it.

### Open questions — resolved

1. **Hot-reload semantics for in-flight sessions** — *honour on next turn*.
   Recorded in step 4 above.
2. **Settings audit trail** — *deferred until there's a shared-tenant story*.
   For single-user desktop / one-admin server installs the value is modest
   (settings rarely change; one person makes the change). For multi-admin
   server deployments compliance traceability becomes a real win — defer
   that work to a future "team installations" milestone. The data model is
   purely additive (separate `settings_audit` table; no consumer changes),
   so deferring doesn't lock anything in.
3. **Schema validation on `SaveAsync`** — *yes, write-time validation*.
   Each `SaveToStoreAsync` enforces shape constraints (enum values, regex
   compilability, non-negative integers, valid mode strings) and throws
   `ArgumentException` rather than persisting bad data. The resolver's
   read-time permissiveness stays as a safety net for legacy / hand-edited
   rows. Step 3 introduces this for `TrustBoundaryConfig.SaveToStoreAsync`;
   step 2's `GovernanceConfig.SaveToStoreAsync` is retro-fitted in the same
   step for consistency.

