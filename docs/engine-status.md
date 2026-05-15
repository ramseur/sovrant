# Sovrant Engine — Status Report

**Branch:** `sovrant-openc-dotnet-port`
**Last updated:** 2026-05-15 (56 tools, 115 server endpoints + SignalR hub, 1,689 tests across 10 projects, JS SDK covering the server, V001–V026 migrations, Phase 93 Configuration Boundary Audit shipped, v0.9.2 release candidate)
**Test models:** `gemini-2.5-flash` (Google AI Studio, free tier), `gpt-4o-mini` (OpenAI, paid tier)

---

## Engine Core

| Component | Status | Notes |
|---|---|---|
| CLI entry point (`sovrant prompt "..."`) | ✅ Working | One-shot mode confirmed |
| REPL loop (`sovrant`) | ✅ Working | Slash commands, history, Spectre.Console rendering |
| SmartRouter | ✅ Working | Pings providers on startup, routes by latency/cost/health. Falls back to configured providers when all fail startup ping (WSL/CI DNS resilience). |
| Agentic loop | ✅ Working | Multi-turn tool use, up to 20 rounds per turn |
| Session persistence (SQLite) | ✅ Working | `~/.sovrant/data/sovrant.db` — sessions + session_entries tables with FTS5 search. Legacy JSONL dual-write via `SOVRANT_SESSION_JSONL=true`. |
| Session resumption (`--session <id>`) | ✅ Working | History replayed correctly across separate process invocations |
| Permission system | ✅ Working | `bypassPermissions` / `dontAsk` / `default` / `plan` all functional. Phase 59 refactored `ModeAwarePermissionPolicy` to use graduated tool tiers — `DontAsk` mode now requires confirmation for Dangerous/Escalation tools. |
| SSE streaming | ✅ Working | Text chunks stream to console in real time |
| Token counts | ✅ Fixed | OpenAI trailing usage chunk now captured. Input + output tokens reported correctly after each turn. |
| HTTP server (`Sovrant.Server`) | ✅ Working | 115 endpoints + SignalR hub (incl. `/v1/command-center/state` for Phase 90 cockpit): health, chat, config, status, models, sessions (CRUD + config + export), usage, cost, webhook, MCP auth, evals, swarm, users (CRUD + sessions + usage + audit), workspaces (CRUD + members + invites + config + usage + memory), projects (CRUD + archive + members + config + sessions + usage + memory), teams (CRUD + members + runs), runs, missions (CRUD + run + events + export), engine (trace + in-flight + recover + delete), artifacts (list + download + delete), registries (tools + skills + agent templates). SignalR ChatHub at `/hubs/chat` for real-time streaming (Phase 61). |
| Server session pool (`IRuntimeSessionPool`) | ✅ Implemented | One `ConversationRuntime` per session ID with per-session `SemaphoreSlim` lock, `SessionConfig` overlay, token accumulators. TTL eviction + LRU cap via `SessionEvictionService`. |
| Session-scoped config | ✅ Implemented | Per-session model + permission mode overlays via `SessionConfig`. `EnterPlanMode`/`ExitPlanMode` scoped to current session via `AsyncLocal`. `PUT /v1/sessions/{id}/config` for explicit overrides. |
| Per-session rate limiting | ✅ Implemented | ASP.NET Core `RateLimiter` keyed on `X-Session-Id` header or client IP. `SOVRANT_RATE_LIMIT_RPM` env var (default 60). Returns 429 when exceeded. |
| Token usage tracking | ✅ Implemented | Per-session `TotalInputTokens`/`TotalOutputTokens` accumulated in `SessionConfig`. `GET /v1/usage` summary. `GET /v1/sessions/{id}` includes totals. |
| LSP integration (`Sovrant.Lsp`) | ✅ Implemented | `ILspClient` / `LspClient` — JSON-RPC 2.0 over stdio, Content-Length framing. `LspClientManager` maps file extensions to language servers. 5 tools: LspHover, LspDefinition, LspReferences, LspDiagnostics, LspRename. Config via `lsp_servers` in `SovrantConfig`. |
| CI/CD integration | ✅ Implemented | `--ci` flag on CLI: JSON output, non-zero exit on error, `CiPermissionPolicy`, `CiUserInputProvider`. GitHub Actions composite action. GitLab CI template in docs. |
| Webhook integration | ✅ Implemented | `POST /v1/webhook` — generic endpoint for Slack, Teams, Discord, custom. Sync or async (callback URL). `WebhookCallbackService` for background delivery. Slack bot at `integrations/slack/`. |
| Frontend SDK | ✅ Implemented | `sdk/js/` — TypeScript `SovrantClient` covering the 115-endpoint server (incl. `updateTeamProfile` for team profiles + Command Center state endpoint), SSE parser, React `useChat()` hook, 75+ type definitions |
| Structured diff view | ✅ Implemented | `DiffRenderer` in CLI — color unified diffs for edit/write tools in REPL |
| Session export | ✅ Implemented | `GET /v1/sessions/{id}/export` — markdown rendering of full session history |
| MCP server mode | ✅ Implemented | `sovrant mcp-server` — stdio transport (JSON-RPC 2.0). Bridges all `IToolRegistry` tools + synthetic `chat` tool + session/config resources to MCP protocol. Zero overlap with HTTP server. Bearer token auth via `SOVRANT_MCP_TOKEN` + `--token`. |
| Dynamic MCP Tool Proxy (`MCPTool`) | ✅ Implemented | Calls any tool on any connected MCP server dynamically at execution time — no static registration needed. Optional `server` param; searches all clients when omitted. |
| SQLite persistence layer | ✅ Implemented | `IStorageProvider` + `SqliteStorageProvider` + 26 versioned migrations V001–V026. Stores: sessions (+ titles, entry provider), memory, audit, credentials, token usage, workspaces, projects, teams (+ run profile), missions, swarm events, coordination events, hooks, workspace_settings, MCP/LSP servers, user preferences, provider profiles. See [persistence.md](persistence.md). |
| Unit test suite | ✅ 1,689 passing | 10 projects: Runtime, Agents, Tools, Server, Api, Runtime.Documents, Commands, Mcp, Lsp, Integration |
| Cost tracking (Phase 55) | ✅ Implemented | `ICostModel`, `OpenRouterCostModel`, `BudgetEnforcer`, `CostMetricsLogger` (JSONL), `/cost` CLI command, `GET /v1/cost` API, cost display in Desktop + Web, `RuntimeEvent.TurnCost`. |
| Inter-agent coordination (Phase 57) | ✅ Implemented | `GroupMailbox`, `PMCoordinator`, `LlmPMAgent`, `CoordinationStatusTool`. SQLite V013 migration (`coordination_events`, `group_pm_assignments`). Enables team-to-team and swarm-to-swarm coordination through leader/PM agents. |
| Remote server mode (Phase 61) | ✅ Implemented | SignalR `ChatHub` at `/hubs/chat`, `RuntimeEventDto` shared DTO, `AddSovrantClient()` DI extension. Web frontend can run in embedded mode (in-process) or remote mode (connecting to Sovrant.Server via SignalR). Controlled by `SOVRANT_RUNTIME_MODE=embedded\|remote`. |
| Phase 7.5 Tier 1 tools | ✅ Implemented | TaskUpdate, EnterPlanMode, ExitPlanMode, EnterWorktree, ExitWorktree |
| Phase 7.5 Tier 2 tools | ✅ Implemented | Skill, ToolSearch, ListMcpResources, ReadMcpResource + custom project slash commands + `/memory` command |
| Phase 7.6 memory files | ✅ Implemented | `~/.sovrant/memory.md` + `.sovrant/memory.md` injected into system prompt at session start |
| Phase 17.5 agent scaffolding | ✅ Implemented | `Sovrant.Agents` project: `IAgent`, `IOrchestrationSystem`, dual backends (isolated + shared), `AGENT_MODE` config switch, `SovrantAgentFactory`, `AgentPrompts`, `FilteredToolRegistry`. Wired into CLI and Server DI via `AddOrchestrationSystem()`. |
| Phase 18+19 orchestrated teams | ✅ Implemented | `ITeamRegistry` + `InMemoryTeamRegistry`, 4 team tools (`TeamCreate`, `TeamDelete`, `TeamStatus`, `TeamDelegate`), `OrchestrationCoordinator` (semaphore concurrency, linked CTS + timeout), `ProcessAgent` (stdin/stdout, process tree kill), `SovrantAgent` (runtime-backed), 6 role-specific `AgentPrompts`. 58 tests in `Sovrant.Agents.Tests`. |
| Eval framework (Phase 27) | ✅ Implemented | 3 grader types (code, model, human), pass@1 + pass@k metrics, JSON eval definitions in `.sovrant/evals/`, trend tracking via `EvalResultStore`, `/eval` command, 3 server endpoints. 62 tests. |
| Swarm orchestrator (Phase 28) | ✅ Implemented | Auto-decomposition via LLM, Kahn's topological sort for wave assignment, wave-by-wave parallel execution (`SemaphoreSlim`), pessimistic file locking, token budget enforcement, per-task retry + timeout, optional quality gate, JSONL session recording, team bridge (different orchestrations use different teams). OFF by default. `SwarmTool` + `SwarmStatusTool`, `/swarm` command, 4 server endpoints (SSE streaming). 62 tests. |
| OpenAI Responses API provider | ✅ Implemented + tested | `OpenAiResponsesProvider` routes through `POST /v1/responses` when `LLM_WEB_SEARCH=true`. Injects `web_search_preview`, suppresses `WebSearch` function tool, full multi-turn agentic loop support. |
| Phase 7 hardening | ✅ Complete | Context auto-compaction (`SOVRANT_COMPACT_THRESHOLD`, default 80k tokens); BashTool 256 KB cap + dangerous env stripping; WebFetchTool SSRF guard (RFC-1918, loopback, link-local, non-HTTP(S)); provider retry 3×(1s/2s/4s) on 429/5xx; AgentTool recursion depth ≤ 5; ReadFileTool 10 MB cap; GlobTool 1000-file cap; atomic writes in Write/Edit tools. |
| Document generation (Phase 66) | ✅ Implemented | `IDocumentGenerator` + 6 generators (Markdown / PDFsharp / MigraDoc structured PDF / OpenXml Word / ClosedXML Excel / OpenXml PowerPoint) and a registry. 44 industry templates across 7 verticals (business, finance, legal, real-estate, healthcare, education, construction). Agent tools: `DocumentGenerate`, `DocumentFromTemplate`, `DocumentListTemplates`, `DocumentSuggestTemplate`, `DocumentPackage`, `DocumentListPackages`. `IDocumentTrustGate` (default `HealthcarePhiTrustGate`) refuses healthcare templates without explicit PHI consent. In-chat document cards in Web (`DocumentArtifactCard.razor`, with PDF iframe preview) and Desktop (`DocumentArtifactViewModel`, with Open + Reveal). Workspace-scoped user templates deferred to Phase 74. 84 tests. |
| Autonomous-driver layer (Phase 67) | ✅ Implemented | `IAutonomousDriver` + `DriverCapabilities` + `DriverRegistry` seam in `Sovrant.Runtime/Missions/`. `LlmAutonomousDriver` (name: `"llm"`) wraps `IMissionExecutor`; `SwarmAutonomousDriver` (name: `"swarm"`) decomposes + orchestrates and projects `SwarmEvent`s onto `mission_events` under a stable `swarm_*` type vocabulary. `LlmMissionExecutor` remains the default mission execution path — the driver layer is additive. |
| Foundations hardening (Phase 68) | ✅ Partial | `SovrantException` base in `Sovrant.Api.Errors` — `ApiError`, `MacroExpansionException`, `TemplateValidationException`, `MigrationDriftException` re-parented. DI-singleton registries (`InMemoryToolRegistry`, `AgentTemplateRegistry`) moved to `ConcurrentDictionary` with concurrent-writer tests. Full-source audit confirmed every public async method in `Sovrant.Runtime` already takes `CancellationToken`. DI sweep, logging taxonomy, cold-start profiling, broader catch-site audit remain. |
| Team run profiles (Phase 78 Path 2) | ✅ Implemented | V015 migration adds `run_mode`, `max_concurrent`, `file_locks_enabled`, `quality_gate_enabled`, `quality_gate_threshold`, `decomposition_mode` to `teams`. `PUT /v1/teams/{id}/profile` endpoint with PATCH-style partial updates and snake_case JSON binding. `TeamRunner` honours the profile (sequential vs parallel execution, concurrency cap, file-lock arbitration, quality gate threshold, decomposition mode). SDK exposes `updateTeamProfile()`. Editable from Web/Desktop Orchestration page. |
| Command Center cockpit (Phase 89/90) | ✅ Implemented | `/command` page on Web (`Sovrant.Web/Components/Pages/CommandCenter.razor`) and Desktop (`CommandCenterView.axaml` + `CommandCenterViewModel`) — read-only live grid aggregating active missions, team runs, agent runs, and sessions for the current user/workspace. Backed by `CommandCenterAggregator` and `GET /v1/command-center/state`. 2-second auto-refresh; click-through to Activity / Orchestration / mission detail. Default landing page after first-run setup. |
| Public release readiness (Phase 90) | ✅ Shipped 2026-05-02 | README repositioned around source-available BSL 1.1 framing; provider API keys migrated through credential keystore (no plaintext on disk); inline-style cleanup; sortable parameter tables on Tools page; Activity drill-down with per-turn detail; `/agents` "Run now" via `AdHocAgentRunner`; Automations stub removed in favour of MCP-platform integrations. |
| v0.9.2 release candidate | ✅ Prepared 2026-05-15 | License Change Date moved to 2029-05-15; cross-user provider profile leakage fixed; workspace provider profiles wired into model/provider dropdown (admin-added keys visible to workspace members); Settings API key field starts blank on every load; admin registration toggles fixed on Web; assembly version bumped to 0.9.2. Tag held until UAT confirms. |

### Known issues fixed during testing

| Issue | Fix |
|---|---|
| Provider URL: hardcoded `/v1/chat/completions` overrode base URL path | Changed to relative `chat/completions`; base URL normalised to always have trailing slash |
| `ProviderApiProvider` (Anthropic `/v1/messages` format) was always registered and routed alongside `OpenAiCompatProvider` | Now only registered when `PROVIDER_BASE_URL` env var is explicitly set |
| `--permission-mode bypass-permissions` (hyphen) silently fell back to `Default` | Use `bypassPermissions` (camelCase) — matches the `PermissionMode` enum |
| `--session` option was parsed but never wired to `InitializeSessionAsync` | Fixed: session ID now applied to the same `IConversationRuntime` instance used for the turn |
| `DisableFastUpToDateCheck` missing — MSB3492 cache file race on parallel Windows builds | Added to `Directory.Build.props` |
| `ConversationRuntime` set `Stream=false` on internal `MessagesRequest` | Fixed: runtime always sets `Stream=true`; server buffers or forwards SSE independently |
| Server ran stale binary (pre-URL-fix) during smoke test — ping URL was `v1/v1/models` → 404, all providers unhealthy | Always rebuild server before smoke testing: `dotnet build src/Sovrant.Server` |

### Known open issues

| Issue | Details |
|---|---|
| ~~Token counts always show `0↑ 0↓`~~ | ✅ Fixed — `OpenAiCompatProvider` captures trailing OpenAI usage chunk; runtime reads `InputTokens` from `MessageDelta`. |
| ~~SmartRouter crashes when all providers fail startup ping~~ | ✅ Fixed — falls back to configured providers when all fail ping; `ConversationRuntime` catches routing exception and emits `RuntimeError` instead of crashing. |
| `AskUserQuestion` blocked in server mode | Returns a fixed "question blocked" message — by design; interactive prompts not possible in HTTP server context. |
| `launchSettings.json` / port conflict on rapid server restart | `src/Sovrant.Server/Properties/launchSettings.json` declares port `5091` that Kestrel overrides with `5200`. Rapid restart causes `SocketException (10048)`. **Mitigation:** always `pkill -f Sovrant.Server` first. **Fix (Phase 9):** align `launchSettings.json` port with `SOVRANT_PORT` and add `--urls` override for CI. |
| ~~`EnterPlanMode` / `ExitPlanMode` are global in server mode~~ | ✅ Fixed — `SessionConfig` overlay makes plan mode per-session via `AsyncLocal`. `PUT /v1/sessions/{id}/config` for explicit overrides. |
| ~~No provider retry on 429 / 5xx~~ | ✅ Fixed — 3 attempts with 1s/2s/4s backoff on retryable errors in `ConversationRuntime`. |
| ~~`AgentTool` has no recursion depth limit~~ | ✅ Fixed — `AsyncLocal<int>` counter; rejects at depth ≥ 5. |
| ~~`Sovrant.Agents` not wired into CLI or Server~~ | ✅ Fixed — `AddOrchestrationSystem()` called in both CLI and Server `Program.cs`. Team tools registered. `AgentTool` uses direct `ConversationRuntime` (by design — lightweight ad-hoc). |

### Phase 8 — Structured Async Logging ✅

| Item | Status |
|---|---|
| Async rolling file logger (`AsyncRollingFileLoggerProvider`) | ✅ Custom non-blocking implementation using `System.Threading.Channels` — daily rolling, bounded 4096-entry channel, `DropOldest` backpressure |
| `SOVRANT_LOG_LEVEL` / `SOVRANT_LOG_FILE` / `SOVRANT_LOG_CONSOLE` / `SOVRANT_LOG_FORMAT` env vars | ✅ `SovrantLogConfig.FromEnvironment()` |
| Wired in CLI and Server | ✅ `AddSovrantLogging()` in both `Program.cs` files |
| `[LoggerMessage]` source-generated delegates | ✅ 22+ delegates across `ConversationRuntime`, `SmartRouter`, `OpenAiCompatProvider`, `DefaultToolExecutor`, `RequestLoggingMiddleware`, `JsonlSessionStore`, `McpToolRegistrar`, `ServerLog` |
| Ambient context (`session_id`, `model`, `turn`) via `BeginScope()` | ✅ In `ConversationRuntime.RunTurnAsync` |
| Stopwatch timing on tool dispatch | ✅ `duration_ms` logged on every tool completion |
| All critical log points from roadmap | ✅ Turn start/complete, tool dispatch/result, retry, compaction, provider selection, provider health, SSE errors, permission denied, request pipeline |
| Structured JSON output (`SOVRANT_LOG_FORMAT=json`) | ✅ Includes scope properties (`session_id`, `model`, `turn`) in JSON log lines |
| Integration test for structured log output | ✅ `StructuredLoggingTests` — verifies `session_id` on turn-start and scope propagation |
| No inline `_logger.LogXxx(...)` calls remaining | ✅ All converted to `[LoggerMessage]` delegates |

### Phase 9 — Multi-Tenant Per-Request Credentials ✅

| Item | Status |
|---|---|
| `X-LLM-Api-Key` / `X-LLM-Base-Url` headers on chat request | ✅ Read from request headers |
| `ScopedSingleProviderRouter` | ✅ Lightweight `ISmartRouter` wrapping one provider — no ping, no health scoring |
| Request-scoped `OpenAiCompatProvider` | ✅ Built from `x_api_key` + `x_base_url` per request; `IHttpClientFactory` named client |
| Composite session pool key (`{session_id}::{provider}`) | ✅ Isolates sessions by provider when per-request credentials present |
| `RuntimeSessionPool.GetOrCreateAsync` scoped router override | ✅ Optional `ISmartRouter` param for creating scoped runtimes |
| `X-LLM-Api-Key` never logged or persisted | ✅ Only passed to `ApiKeyAuthProvider` for auth headers; not in any log path |
| Global config not mutated by scoped requests | ✅ `serverConfig.Model` only updated when NOT using scoped credentials |
| Tests: `ScopedSingleProviderRouterTests` (5) + `RuntimeSessionPoolTests` (5) | ✅ 10 new tests |

### Phase 9.1 — Session Lifecycle Management ✅

| Item | Status |
|---|---|
| `PooledSession` record (runtime + `SemaphoreSlim` lock) | ✅ Returned by `GetOrCreateAsync`; callers acquire lock before `RunTurnAsync` |
| Per-session `SemaphoreSlim(1,1)` turn serialization | ✅ `ChatRoutes` acquires/releases lock around turn execution |
| `SessionEntry` with `LastAccess` timestamp | ✅ Updated on every `GetOrCreateAsync` call |
| `SessionEvictionService` (`IHostedService`) | ✅ Timer sweep every 5 min: TTL eviction + LRU cap enforcement |
| `SOVRANT_SESSION_TTL_SECONDS` env var (default: `3600`) | ✅ |
| `SOVRANT_MAX_SESSIONS` env var (default: `500`) | ✅ |
| `EvictExpired(ttl, maxSessions)` on `IRuntimeSessionPool` | ✅ Two-phase: TTL sweep then LRU cap |
| `ActiveCount` property on pool | ✅ |
| `GET /v1/status` includes `active_sessions`, `max_sessions`, `session_ttl_seconds` | ✅ |
| Lock disposed on `Evict()` and lost-race cleanup | ✅ |
| Tests: locking, TTL eviction, LRU cap, active count | ✅ 4 new tests (9 total RuntimeSessionPoolTests) |

### Phase 58 — Sovrant Trust Boundary ✅

| Item | Status |
|---|---|
| `TrustBoundaryProvider` — `ILlmProvider` decorator wrapping 3-stage pipeline | ✅ Sanitize outbound → forward → restore inbound → ethical scan |
| `IPromptSanitizer` / `PromptSanitizer` — sanitizes all text content in `MessagesRequest` | ✅ System prompt, TextBlock, ToolResultBlock.TextBlock, ToolUseBlock JSON input |
| `RedactionMap` — bidirectional original↔placeholder mapping, scoped per request | ✅ Deterministic `[CATEGORY_N]` naming, never persisted |
| `PiiDetector` — regex-based PII detection (email, phone, SSN, card, internal IP) | ✅ GeneratedRegex, public IPs excluded |
| `CorporateDataDetector` — connection strings, API keys, cloud ARNs, internal hostnames | ✅ Configurable domains + allow-list |
| `CustomPatternRegistry` — user-defined regex patterns from config | ✅ Category = uppercase name |
| `IEthicalHarness` / `ContentPolicyEngine` — rule-based harmful content classifier | ✅ Standard/Strict/Enterprise strictness, 6+ categories, response scanning |
| `EthicalAuditLog` — thread-safe in-memory compliance log | ✅ `ConcurrentQueue` with max capacity eviction |
| `IntentVerificationBridge` — connects Phase 59's `IIntentGate` as first trust stage | ✅ Ethical harness runs before intent gate |
| `TrustBoundaryConfig` — root config with Sanitizer, EthicalHarness, IntentVerification | ✅ Wired into `SovrantConfig` |
| DI wiring — all trust boundary services registered in `ServiceCollectionExtensions` | ✅ |
| Tests — 7 test files (72+ tests): PII, corporate data, redaction map, sanitizer, ethical engine, intent bridge, provider | ✅ |

### Phase 59 — Agentic Loop Hardening ✅

| Item | Status |
|---|---|
| `IIntentGate` / `SemanticIntentGate` — semantic intent classification replacing `LooksLikeToolRequest()` | ✅ Wraps existing `IntentClassifier`, adds RequiresTools + NeedsClarification logic |
| `GraduatedToolTiers` — classifies all 49+ tools into Safe/Moderate/Dangerous/Escalation tiers | ✅ Static classification |
| `IPlanPresenter` / `PlanPresenter` — formats plans as numbered step lists with destructive warnings | ✅ |
| `PlanApprovalGate` — AlwaysApprove / ApproveDestructive / AlwaysAsk modes | ✅ |
| `StepToolEnforcer` — per-step tool allow-lists | ✅ |
| `IntentInjector` — appends step intent and allowed tools to system prompt | ✅ |
| `ExecutionBudget` — max tool calls, max files modified, max execution time | ✅ |
| `IOrchestrationRouter` / `HeuristicOrchestrationRouter` — recommends Direct/SubAgent/Team/Swarm/Mission | ✅ |
| `PlanProgressTracker` — emits `StepProgress` events on step start/complete | ✅ |
| `ModeAwarePermissionPolicy` refactored — `DontAsk` uses graduated tiers | ✅ Safe/Moderate auto-approve, Dangerous requires confirmation |
| `RuntimeEvent.ClarificationNeeded` — emitted when intent is ambiguous | ✅ Wired into CLI, Desktop, Web, Server |
| `RuntimeEvent.PlanPresented` — emitted after plan creation | ✅ Wired into CLI, Desktop, Web, Server |
| `RuntimeEvent.StepProgress` — emitted on step start/complete | ✅ Wired into CLI, Desktop, Web, Server |
| Server SSE — `SovrantEvent` extension fields for Phase 59 events | ✅ `clarification`, `plan_id`, `formatted_plan`, `requires_approval`, `step_current/total/intent/status` |

---

## Tools — Test Results

Core tools tested with `gpt-4o-mini` (paid tier), `--permission-mode bypassPermissions`.
File tools also confirmed with `gemini-2.5-flash` (free tier, rate-limited).

### Core file tools

| Tool | Status | Result |
|---|---|---|
| `Read` | ✅ Tested | Reads file contents correctly |
| `Write` | ✅ Tested | Creates file with specified content |
| `Edit` | ✅ Tested | String replacement in existing file confirmed |
| `Glob` | ✅ Tested | Pattern match returns correct file list |
| `Grep` | ✅ Tested | Regex search across files works correctly |
| `LS` | ✅ Tested | Directory listing returned correctly |

### Shell tools

| Tool | Status | Result |
|---|---|---|
| `Bash` | ✅ Tested | Tool fires and executes. **Windows caveat:** requires WSL installed and updated (`wsl.exe --update`). Works on Linux/macOS natively |
| `PowerShell` | ⬜ Not tested | Implemented via `pwsh.exe` — should work on Windows with PowerShell 7 |
| `REPL` | ⬜ Not tested | Implemented; spawns subprocess per language (`python`, `node`, etc.) |

### Web tools

| Tool | Status | Result |
|---|---|---|
| `WebFetch` | ✅ Tested | Fetched `https://httpbin.org/get`; model correctly extracted response data |
| `WebSearch` | ⬜ Not tested | Implemented; requires `BRAVE_API_KEY` (or `FIRECRAWL_API_KEY` as fallback) |
| Native web search (`LLM_WEB_SEARCH=true`) | ✅ Tested | Routes through OpenAI Responses API (`/v1/responses`); `web_search_preview` built-in tool injected; `WebSearch` function tool suppressed; no Brave/FireCrawl key required |

### Task management tools

| Tool | Status | Result |
|---|---|---|
| `TodoWrite` | ✅ Tested | Created 2-item task list; model confirmed both items with priority |
| `TaskCreate` | ⬜ Not tested | Implemented; spawns background `dotnet` sub-process |
| `TaskGet` | ⬜ Not tested | Implemented; polls `BackgroundTaskRegistry` by task ID |
| `TaskList` | ⬜ Not tested | Implemented; lists all tracked background tasks |
| `TaskOutput` | ⬜ Not tested | Implemented; streams stdout from running background task |
| `TaskStop` | ⬜ Not tested | Implemented; cancels and removes background task |
| `TaskUpdate` | ⬜ Not tested | Implemented (Phase 7.5); updates task description |

### Agent & interaction tools

| Tool | Status | Result |
|---|---|---|
| `Agent` | ⬜ Not tested | Implemented; spawns isolated `ConversationRuntime` with its own session. Recursion depth ≤ 5. |
| `AskUserQuestion` | ✅ Tested | Prompted console correctly in CLI mode. Server mode returns fixed message (by design) |
| `Sleep` | ✅ Tested | Slept 1000ms and returned correctly |

### Team orchestration tools *(Phase 18+19)*

| Tool | Status | Result |
|---|---|---|
| `TeamCreate` | ⬜ Not tested | Implemented; creates named agent with role, custom prompt, optional tool restrictions and model override |
| `TeamDelete` | ⬜ Not tested | Implemented; cancels agent tasks and removes from registry |
| `TeamStatus` | ⬜ Not tested | Implemented; returns JSON array of all team members with lifecycle state |
| `TeamDelegate` | ⬜ Not tested | Implemented; delegates prompt to a team member via `IOrchestrationSystem`, tracks status/output/errors |

### Plan mode tools *(Phase 7.5 Tier 1)*

| Tool | Status | Result |
|---|---|---|
| `EnterPlanMode` | ⬜ Not tested | Implemented; sets `IPermissionModeAccessor.Mode = Plan`. CLI: updates `MutableCliPermissionPolicy`. Server: updates `MutableServerConfig` via adapter |
| `ExitPlanMode` | ⬜ Not tested | Implemented; restores permission mode; optional `permission_mode` param (default: `DontAsk`) |

### Worktree tools *(Phase 7.5 Tier 1)*

| Tool | Status | Result |
|---|---|---|
| `EnterWorktree` | ⬜ Not tested | Implemented; runs `git worktree add`, records path in `WorktreeState` singleton; `create_branch` param for `-b` flag |
| `ExitWorktree` | ⬜ Not tested | Implemented; runs `git worktree remove`, clears `WorktreeState`; `force` param for `--force` |

### Skill & discovery tools *(Phase 7.5 Tier 2)*

| Tool | Status | Result |
|---|---|---|
| `Skill` | ⬜ Not tested | Implemented; reads `.sovrant/skills/{name}.md` (project-first, then global); substitutes `$ARGUMENTS` |
| `ToolSearch` | ⬜ Not tested | Implemented; searches registered tool names/descriptions by keyword via `IToolRegistry.GetDefinitions()` |

### MCP resource tools *(Phase 7.5 Tier 2)*

| Tool | Status | Result |
|---|---|---|
| `ListMcpResources` | ⬜ Not tested | Implemented; lists resources from connected MCP servers via `McpClientRegistry` |
| `ReadMcpResource` | ⬜ Not tested | Implemented; reads a resource by URI from a connected MCP server |

### Notebook tools

| Tool | Status | Result |
|---|---|---|
| `NotebookEdit` | ⬜ Not tested | Implemented; reads/writes Jupyter `.ipynb` JSON; cell replace/insert/delete |

---

## Provider Compatibility

| Provider | Tool Calling | Notes |
|---|---|---|
| `gemini-2.5-flash` (Google AI Studio) | ✅ Confirmed | Free tier: ~5 RPM. All core tools tested |
| `gpt-4o-mini` (OpenAI) | ✅ Confirmed | All tested tools pass; session continuity confirmed |
| `gemma-4-31b-it` (Google AI Studio) | ❌ No tool calls | Text generation works; function calling not supported via OpenAI-compat endpoint |
| `gemma-3-27b-it` (Google AI Studio) | ⬜ Not tested | Likely same limitation as Gemma 4 |
| Ollama (local) | ⬜ Not tested | Implemented; set `OLLAMA_BASE_URL`. Bash tool requires WSL/Linux |
| Native messages API (`ProviderApiProvider`) | ⬜ Not tested | Set `PROVIDER_BASE_URL=https://api.anthropic.com` + `PROVIDER_API_KEY` |

---

## Environment Variables

| Variable | Required | Description |
|---|---|---|
| `LLM_API_KEY` | Yes | API key for the primary provider. Aliases: `OPENAI_API_KEY`, `PROVIDER_API_KEY` (checked in order) |
| `LLM_BASE_URL` | No | Base URL (default: `https://api.openai.com/v1`). Alias: `OPENAI_BASE_URL` |
| `SOVRANT_TOKEN` | Yes (server only) | Bearer token for `Sovrant.Server`. All requests return 401 if unset. |
| `SOVRANT_PORT` | No | Server port (default: `5200`) |
| `PROVIDER_BASE_URL` | No | Enables the native messages API provider (`/v1/messages` format, e.g. `https://api.anthropic.com`) |
| `PROVIDER_API_KEY` | No | API key for the native messages API provider |
| `OLLAMA_BASE_URL` | No | Enables the local Ollama provider (default when set: `http://localhost:11434/v1`) |
| `ROUTER_MODE` | No | `Smart` (default) or `Fixed`. Overrides `Router:Mode` in config. |
| `ROUTER_STRATEGY` | No | `Balanced` (default), `Latency`, or `Cost`. Overrides `Router:Strategy` in config. |
| `AGENT_MODE` | No | `isolated` (default, process-per-agent stdio) or `shared` (in-process async channels). Controls the `IOrchestrationSystem` backend used by team tools. |
| `SOVRANT_MCP_TOKEN` | No | Required bearer token for MCP server mode. If set, callers must pass `--token <value>` matching this. Unset = no auth. |
| `SOVRANT_MCP_TOOLS` | No | Comma-separated allow-list of tool names to expose via MCP server. Unset = all tools. `chat` always passes. |
| `LLM_WEB_SEARCH` | No | Set to `true` to use the model's native web search capability (e.g. OpenAI `web_search_preview`). No external API key needed. |
| `BRAVE_API_KEY` | No | Enables `WebSearch` via Brave Search API |
| `FIRECRAWL_API_KEY` | No | Enables `WebSearch` via FireCrawl (fallback if `BRAVE_API_KEY` not set) |

---

## Server Smoke Test

> All 9 endpoints confirmed working. Two bugs found during initial testing are now fixed:
> - `ConversationRuntime` always sets `Stream=true` internally (was `false`, caused empty response bodies)
> - Stale server binary had double `/v1/v1/` in ping URL — always rebuild before testing
>
> Always build fresh before smoke testing:
> ```bash
> dotnet build src/Sovrant.Server -c Debug
> ```

```bash
export LLM_API_KEY="..."    # fresh key — never paste keys into chat
export SOVRANT_TOKEN=test123

# Start server
dotnet run --project src/Sovrant.Server --no-build &
sleep 5

# 1. Health (unauthenticated)
curl -s http://localhost:5200/health
# expected: {"status":"ok"}

# 2. Non-streaming chat
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Reply with one word: pong"}],"model":"gpt-4o-mini","stream":false}'
# expected: {"choices":[{"message":{"content":"pong",...},...}],...}

# 3. Streaming chat (SSE)
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"Reply with one word: pong"}],"model":"gpt-4o-mini","stream":true}'
# expected: data: {...,"delta":{"content":"pong"},...}  then  data: [DONE]

# 4. Session continuity via server pool
curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"My name is Eric"}],"model":"gpt-4o-mini","session_id":"test-session-1"}'

curl -s -X POST http://localhost:5200/v1/chat/completions \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"messages":[{"role":"user","content":"What is my name?"}],"model":"gpt-4o-mini","session_id":"test-session-1"}'
# expected: second response references "Eric"

# 5. Status endpoint
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/status

# 6. Models endpoint
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/models

# 7. Config update
curl -s -X PUT http://localhost:5200/v1/config \
  -H "Authorization: Bearer test123" \
  -H "Content-Type: application/json" \
  -d '{"model":"gpt-4o"}'

# 8. Session list
curl -s -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions

# 9. Session delete
curl -s -X DELETE -H "Authorization: Bearer test123" http://localhost:5200/v1/sessions/test-session-1
```

## Tools Needing Smoke Tests

The following tools are implemented but have not been manually tested end-to-end with a live LLM:

| Tool | Notes |
|---|---|
| `PowerShell` | Requires PowerShell 7 (`pwsh`) on Windows |
| `REPL` | Spawns subprocess per language (`python`, `node`, etc.) |
| `WebSearch` | Requires `BRAVE_API_KEY` or `FIRECRAWL_API_KEY` |
| `TaskCreate` / `TaskGet` / `TaskList` / `TaskOutput` / `TaskStop` / `TaskUpdate` | Background task management suite |
| `Agent` | Spawns isolated `ConversationRuntime`; recursion depth limited to 5 |
| `TeamCreate` / `TeamDelete` / `TeamStatus` / `TeamDelegate` | Team orchestration tools — require `IOrchestrationSystem` (wired in DI) |
| `EnterPlanMode` / `ExitPlanMode` | Global in server mode until Phase 9.5 |
| `EnterWorktree` / `ExitWorktree` | Requires git repo with at least one commit |
| `Skill` / `ToolSearch` | Requires `.sovrant/skills/` dir or registered tools |
| `ListMcpResources` / `ReadMcpResource` | Requires at least one connected MCP server |
| `NotebookEdit` | Requires a `.ipynb` file |
